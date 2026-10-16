using Microsoft.EntityFrameworkCore;
using KYCSACCO.Data;
using KYCSACCO.Models;
using KYCSACCO.Models.DTOs;

namespace KYCSACCO.Services
{
    public interface ICompanyService
    {
        Task<CompanyResponseDTO> CreateCompanyAsync(CompanyDTO companyDto);
        Task<CompanyResponseDTO> UpdateCompanyAsync(int id, CompanyDTO companyDto);
        Task<bool> DeleteCompanyAsync(int id);
        Task<CompanyResponseDTO> GetCompanyByIdAsync(int id);
        Task<CompanyResponseDTO> GetCompanyByCodeAsync(string companyCode);
        Task<List<CompanyResponseDTO>> GetAllCompaniesAsync(string search = null);
        Task<string> GenerateCompanyCodeAsync();
        Task<List<object>> GetAllCompaniesForDropdownAsync();
        Task<bool> IsCompanyCodeUniqueAsync(string companyCode, int? excludeId = null);
    }

    public class CompanyService : ICompanyService
    {
        private const string KycPrefix = "KYC";

        private readonly ApplicationDbContext _context;
        private readonly IBlockchainService _blockchainService;
        private readonly ILogger<CompanyService> _logger;
        private readonly ICompanyContextService _companyContextService;

        public CompanyService(
            ApplicationDbContext context,
            IBlockchainService blockchainService,
            ILogger<CompanyService> logger,
            ICompanyContextService companyContextService)
        {
            _context = context;
            _blockchainService = blockchainService;
            _logger = logger;
            _companyContextService = companyContextService;
        }

        public async Task<List<object>> GetAllCompaniesForDropdownAsync()
        {
            try
            {
                return await _context.Companies
                    .Where(c => c.CompanyCode.StartsWith(KycPrefix))
                    .OrderBy(c => c.CompanyName)
                    .Select(c => new
                    {
                        c.CompanyCode,
                        DisplayText = $"{c.CompanyCode} - {c.CompanyName}"
                    })
                    .ToListAsync<object>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetAllCompaniesForDropdownAsync");
                return new List<object>();
            }
        }

        public async Task<CompanyResponseDTO> CreateCompanyAsync(CompanyDTO companyDto)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                _logger.LogInformation($"Creating new company: {companyDto.CompanyName}");

                var currentUser = _companyContextService.GetCurrentUserName() ?? "SYSTEM";

                // Ensure the incoming code starts with KYC. If empty, generate one.
                if (string.IsNullOrWhiteSpace(companyDto.CompanyCode))
                {
                    companyDto.CompanyCode = await GenerateCompanyCodeAsync();
                }
                else if (!companyDto.CompanyCode.StartsWith(KycPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        $"Company Code must start with '{KycPrefix}'.");
                }

                if (!await IsCompanyCodeUniqueAsync(companyDto.CompanyCode))
                {
                    throw new InvalidOperationException(
                        $"Company code {companyDto.CompanyCode} already exists.");
                }

                string countyName = null;
                string subCountyName = null;
                string wardName = null;

                var company = new Company
                {
                    CompanyCode = companyDto.CompanyCode,
                    CompanyName = companyDto.CompanyName,
                    Contactperson = companyDto.Contactperson,
                    Telephone = companyDto.Telephone,
                    Email = companyDto.Email,
                    Address = companyDto.Address,
                    NoEmployees = companyDto.NoEmployees,
                    County = countyName ?? companyDto.County,
                    SubCounty = subCountyName ?? companyDto.SubCounty,
                    Ward = wardName ?? companyDto.Ward,
                    Village = companyDto.Village,
                    Cigcode = companyDto.Cigcode,
                    CountyCode = companyDto.CountyCode,
                    Unitcode = companyDto.Unitcode,
                    AccountNo = companyDto.AccountNo,
                    NoYears = companyDto.NoYears,
                    Location = companyDto.Location,
                    Type = companyDto.Type,
                    Capital = companyDto.Capital,
                    Project = companyDto.Project,
                    CSRegNO = companyDto.CSRegNO,
                    AuditId = currentUser,
                    AuditTime = DateTime.Now
                };

                _context.Companies.Add(company);
                await _context.SaveChangesAsync();

                string blockchainTxId = null;
                try
                {
                    var blockchainData = new
                    {
                        CompanyId = company.Id,
                        CompanyCode = company.CompanyCode,
                        CompanyName = company.CompanyName,
                        CSRegNO = company.CSRegNO,
                        Email = company.Email,
                        Contactperson = company.Contactperson,
                        Telephone = company.Telephone,
                        Address = company.Address,
                        Location = company.Location,
                        County = company.County,
                        SubCounty = company.SubCounty,
                        Ward = company.Ward,
                        NoEmployees = company.NoEmployees,
                        Action = "CREATE",
                        CreatedBy = currentUser,
                        CreatedAt = DateTime.Now
                    };

                    var blockchainTx = await _blockchainService.CreateAndAddTransactionAsync(
                        "COMPANY_CREATE",
                        company.CompanyCode,
                        company.CompanyCode,
                        0,
                        company.Id.ToString(),
                        blockchainData);

                    if (blockchainTx != null)
                    {
                        blockchainTxId = blockchainTx.TransactionId;
                        company.BlockchainTxId = blockchainTxId;
                        await _context.SaveChangesAsync();
                        _logger.LogInformation($"Blockchain transaction recorded for company {company.CompanyCode}: {blockchainTxId}");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Failed to record blockchain transaction for company {company.CompanyCode}");
                }

                await transaction.CommitAsync();

                _logger.LogInformation($"Company {company.CompanyCode} created successfully");

                return MapToResponseDTO(company, blockchainTxId);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error creating company");
                throw;
            }
        }

        public async Task<CompanyResponseDTO> UpdateCompanyAsync(int id, CompanyDTO companyDto)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                _logger.LogInformation($"Updating company ID: {id}");

                var company = await _context.Companies.FindAsync(id);
                if (company == null)
                {
                    throw new InvalidOperationException($"Company with ID {id} not found.");
                }

                var currentUser = _companyContextService.GetCurrentUserName() ?? "SYSTEM";

                var oldValues = new
                {
                    company.CompanyCode,
                    company.CompanyName,
                    company.Email,
                    company.Contactperson,
                    company.Telephone,
                    company.Address,
                    company.Location,
                    company.CSRegNO
                };

                // CompanyCode is NOT updated - it's read-only
                company.CompanyName = companyDto.CompanyName;
                company.Contactperson = companyDto.Contactperson;
                company.Telephone = companyDto.Telephone;
                company.Email = companyDto.Email;
                company.Address = companyDto.Address;
                company.NoEmployees = companyDto.NoEmployees;
                company.County = companyDto.County;
                company.SubCounty = companyDto.SubCounty;
                company.Ward = companyDto.Ward;
                company.Village = companyDto.Village;
                company.Cigcode = companyDto.Cigcode;
                company.CountyCode = companyDto.CountyCode;
                company.Unitcode = companyDto.Unitcode;
                company.AccountNo = companyDto.AccountNo;
                company.NoYears = companyDto.NoYears;
                company.Location = companyDto.Location;
                company.Type = companyDto.Type;
                company.Capital = companyDto.Capital;
                company.Project = companyDto.Project;
                company.CSRegNO = companyDto.CSRegNO;
                company.AuditId = currentUser;
                company.AuditTime = DateTime.Now;

                await _context.SaveChangesAsync();

                string blockchainTxId = null;
                try
                {
                    var blockchainData = new
                    {
                        CompanyId = company.Id,
                        Action = "UPDATE",
                        OldValues = oldValues,
                        NewValues = new
                        {
                            company.CompanyName,
                            company.Email,
                            company.Contactperson,
                            company.Telephone,
                            company.Address,
                            company.Location,
                            company.CSRegNO
                        },
                        ModifiedBy = currentUser,
                        ModifiedAt = DateTime.Now
                    };

                    var blockchainTx = await _blockchainService.CreateAndAddTransactionAsync(
                        "COMPANY_UPDATE",
                        company.CompanyCode,
                        company.CompanyCode,
                        0,
                        company.Id.ToString(),
                        blockchainData);

                    if (blockchainTx != null)
                    {
                        blockchainTxId = blockchainTx.TransactionId;
                        company.BlockchainTxId = blockchainTxId;
                        await _context.SaveChangesAsync();
                        _logger.LogInformation($"Blockchain transaction recorded for company update: {blockchainTxId}");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Failed to record blockchain transaction for company update");
                }

                await transaction.CommitAsync();

                _logger.LogInformation($"Company {company.CompanyCode} updated successfully");

                return MapToResponseDTO(company, blockchainTxId);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error updating company");
                throw;
            }
        }

        private CompanyResponseDTO MapToResponseDTO(Company company, string blockchainTxId)
        {
            return new CompanyResponseDTO
            {
                Id = company.Id,
                CompanyCode = company.CompanyCode,
                CompanyName = company.CompanyName,
                Contactperson = company.Contactperson,
                Telephone = company.Telephone,
                Email = company.Email,
                Address = company.Address,
                NoEmployees = company.NoEmployees,
                County = company.County,
                SubCounty = company.SubCounty,
                Ward = company.Ward,
                Village = company.Village,
                Cigcode = company.Cigcode,
                CountyCode = company.CountyCode,
                Unitcode = company.Unitcode,
                AccountNo = company.AccountNo,
                NoYears = company.NoYears,
                Location = company.Location,
                Type = company.Type,
                Capital = company.Capital,
                Project = company.Project,
                CSRegNO = company.CSRegNO,
                AuditId = company.AuditId,
                AuditTime = company.AuditTime,
                CreatedBy = company.AuditId,
                CreatedAt = company.AuditTime ?? DateTime.Now,
                ModifiedBy = company.AuditId,
                ModifiedAt = company.AuditTime,
                BlockchainTxId = blockchainTxId,
                BusinessStatus = "Active"
            };
        }

        public async Task<bool> DeleteCompanyAsync(int id)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var company = await _context.Companies.FindAsync(id);
                if (company == null)
                {
                    throw new InvalidOperationException($"Company with ID {id} not found.");
                }

                var currentUser = _companyContextService.GetCurrentUserName() ?? "SYSTEM";

                var companyDetails = new
                {
                    company.Id,
                    company.CompanyCode,
                    company.CompanyName,
                    company.Email,
                    company.Contactperson,
                    company.Telephone,
                    company.Address,
                    company.Location
                };

                _context.Companies.Remove(company);
                await _context.SaveChangesAsync();

                try
                {
                    var blockchainData = new
                    {
                        Action = "DELETE",
                        CompanyDetails = companyDetails,
                        DeletedBy = currentUser,
                        DeletedAt = DateTime.Now
                    };

                    var blockchainTx = await _blockchainService.CreateAndAddTransactionAsync(
                        "COMPANY_DELETE",
                        company.CompanyCode,
                        company.CompanyCode,
                        0,
                        company.Id.ToString(),
                        blockchainData);

                    _logger.LogInformation($"Blockchain transaction recorded for company deletion: {blockchainTx?.TransactionId}");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Failed to record blockchain transaction for company deletion");
                }

                await transaction.CommitAsync();

                _logger.LogInformation($"Company {company.CompanyCode} deleted successfully");
                return true;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error deleting company");
                throw;
            }
        }

        public async Task<CompanyResponseDTO> GetCompanyByIdAsync(int id)
        {
            var company = await _context.Companies
                .FirstOrDefaultAsync(c => c.Id == id);

            if (company == null) return null;

            return MapToResponseDTO(company, company.BlockchainTxId);
        }

        public async Task<CompanyResponseDTO> GetCompanyByCodeAsync(string companyCode)
        {
            var company = await _context.Companies
                .FirstOrDefaultAsync(c => c.CompanyCode == companyCode);

            if (company == null)
            {
                return null;
            }

            return MapToResponseDTO(company, company.BlockchainTxId);
        }

        // ============================================================
        // Returns all companies whose code starts with KYC.
        // Not filtered by the logged-in user's company.
        // ============================================================
        public async Task<List<CompanyResponseDTO>> GetAllCompaniesAsync(string search = null)
        {
            var query = _context.Companies
                .Where(c => c.CompanyCode.StartsWith(KycPrefix));

            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(c =>
                    c.CompanyCode.Contains(search) ||
                    (c.CompanyName != null && c.CompanyName.Contains(search)) ||
                    (c.Email != null && c.Email.Contains(search)) ||
                    (c.Contactperson != null && c.Contactperson.Contains(search)) ||
                    (c.Telephone != null && c.Telephone.Contains(search)));
            }

            var companies = await query
                .OrderBy(c => c.CompanyName)
                .ToListAsync();

            return companies.Select(c => MapToResponseDTO(c, c.BlockchainTxId)).ToList();
        }

        // ============================================================
        // Generate a new company code with the KYC prefix.
        // Format: KYCyyyyMMdd-NNN  e.g. KYC20261016-001
        // ============================================================
        public async Task<string> GenerateCompanyCodeAsync()
        {
            const string prefix = KycPrefix;                       // "KYC"
            var date = DateTime.Now.ToString("yyyyMMdd");

            // Base looks like: KYCSACCO20261016
            var basePrefix = $"{prefix}SACCO{date}";

            // Format of the full code: KYCSACCOyyyyMMdd-NNN  (e.g. KYCSACCO20261016-001)
            var codePrefixWithDash = $"{basePrefix}-";

            var sequence = 1;

            // Look for the highest existing sequence for today
            var lastCompany = await _context.Companies
                .Where(c => c.CompanyCode.StartsWith(codePrefixWithDash))
                .OrderByDescending(c => c.CompanyCode)
                .FirstOrDefaultAsync();

            if (lastCompany != null && !string.IsNullOrWhiteSpace(lastCompany.CompanyCode))
            {
                // Safely split on the dash — the last segment is the numeric sequence.
                var parts = lastCompany.CompanyCode.Split('-');
                if (parts.Length >= 2)
                {
                    var sequenceStr = parts[^1];                    // last segment: "001", "010", ...
                    if (int.TryParse(sequenceStr, out int lastSeq))
                    {
                        sequence = lastSeq + 1;
                    }
                }
            }

            // Format: KYCSACCO20261016-001 (sequence padded to 3 digits)
            return $"{codePrefixWithDash}{sequence:D3}";
        }

        public async Task<bool> IsCompanyCodeUniqueAsync(string companyCode, int? excludeId = null)
        {
            var query = _context.Companies.Where(c => c.CompanyCode == companyCode);

            if (excludeId.HasValue)
            {
                query = query.Where(c => c.Id != excludeId.Value);
            }

            return !await query.AnyAsync();
        }
    }
}