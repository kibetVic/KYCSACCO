using KYCSACCO.Data;
using KYCSACCO.Services;
using Microsoft.EntityFrameworkCore;
using KYCSACCO.Models;
using KYCSACCO.Models.DTOs;

namespace KYCSACCO.Services
{
    public interface IGIGsService
    {
        Task<GIGsResponseDTO> CreateGIGAsync(GIGsDTO gigDto);
        Task<GIGsResponseDTO> UpdateGIGAsync(int id, GIGsDTO gigDto);
        Task<bool> DeleteGIGAsync(int id);
        Task<GIGsResponseDTO> GetGIGByIdAsync(int id);
        Task<GIGsResponseDTO> GetGIGByCodeAsync(string gigCode);
        Task<List<GIGsResponseDTO>> GetAllGIGsAsync(string search = null);
        Task<string> GenerateGIGCodeAsync(string companyCode);
        Task<bool> IsGIGCodeUniqueAsync(string gigCode, string companyCode, int? excludeId = null);
        Task<List<GIGsResponseDTO>> GetGIGsByCompanyAsync(string companyCode);

        // KYC company lookup for the form dropdown
        Task<List<CompanyLookupDTO>> GetKycCompaniesAsync();
    }

    public class GIGsService : IGIGsService
    {
        // Only companies whose code starts with this prefix are allowed
        private const string KycCompanyPrefix = "KYC";

        private readonly ApplicationDbContext _context;
        private readonly IBlockchainService _blockchainService;
        private readonly ILogger<GIGsService> _logger;
        private readonly ICompanyContextService _companyContextService;

        public GIGsService(
            ApplicationDbContext context,
            IBlockchainService blockchainService,
            ILogger<GIGsService> logger,
            ICompanyContextService companyContextService)
        {
            _context = context;
            _blockchainService = blockchainService;
            _logger = logger;
            _companyContextService = companyContextService;
        }

        // ============================================================
        // Helper: only KYC-prefixed companies are allowed
        // ============================================================
        private static bool IsKycCompany(string companyCode)
        {
            return !string.IsNullOrWhiteSpace(companyCode)
                && companyCode.Trim().StartsWith(KycCompanyPrefix, StringComparison.OrdinalIgnoreCase);
        }

        // ============================================================
        // KYC companies for the dropdown
        // ============================================================
        public async Task<List<CompanyLookupDTO>> GetKycCompaniesAsync()
        {
            return await _context.Companies
                .AsNoTracking()
                .Where(c => c.CompanyCode != null && c.CompanyCode.StartsWith(KycCompanyPrefix))
                .OrderBy(c => c.CompanyName)
                .Select(c => new CompanyLookupDTO
                {
                    Code = c.CompanyCode,
                    Name = c.CompanyName
                })
                .ToListAsync();
        }

        // ============================================================
        // CREATE
        // ============================================================
        public async Task<GIGsResponseDTO> CreateGIGAsync(GIGsDTO gigDto)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var companyCode = gigDto.CompanyCode?.Trim();
                var currentUser = _companyContextService.GetCurrentUserName() ?? "SYSTEM";

                _logger.LogInformation($"Creating new GIG for company: {companyCode}, Name: {gigDto.GigName}");

                if (string.IsNullOrEmpty(companyCode))
                    throw new InvalidOperationException("Company Code is required.");

                if (!IsKycCompany(companyCode))
                    throw new InvalidOperationException(
                        "Only companies with a KYC company code are allowed to register CIGs.");

                var company = await _context.Companies
                    .FirstOrDefaultAsync(c => c.CompanyCode == companyCode);

                if (company == null)
                    throw new InvalidOperationException($"Company '{companyCode}' was not found.");

                var companyName = company.CompanyName;

                if (string.IsNullOrEmpty(gigDto.GigCode))
                {
                    gigDto.GigCode = await GenerateGIGCodeAsync(companyCode);
                }

                if (!await IsGIGCodeUniqueAsync(gigDto.GigCode, companyCode))
                {
                    throw new InvalidOperationException(
                        $"GIG code {gigDto.GigCode} already exists for company {companyCode}.");
                }

                var gig = new GIGs
                {
                    GigCode = gigDto.GigCode,
                    GigName = gigDto.GigName,
                    CompanyCode = companyCode,
                    ContactPhone = gigDto.ContactPhone,
                    ContactEmail = gigDto.ContactEmail,
                    Chairperson = gigDto.Chairperson,
                    RegistrationDate = gigDto.RegistrationDate ?? DateTime.Now,
                    TotalMembers = gigDto.TotalMembers ?? 0,
                    Status = gigDto.Status ?? "Active",
                    CreatedBy = currentUser,
                    CreatedAt = DateTime.Now
                };

                _context.CIGs.Add(gig);
                await _context.SaveChangesAsync();

                string blockchainTxId = null;
                try
                {
                    var blockchainData = new
                    {
                        GigId = gig.Id,
                        GigCode = gig.GigCode,
                        GigName = gig.GigName,
                        CompanyCode = companyCode,
                        CompanyName = companyName,
                        ContactPhone = gig.ContactPhone,
                        ContactEmail = gig.ContactEmail,
                        Chairperson = gig.Chairperson,
                        RegistrationDate = gig.RegistrationDate,
                        TotalMembers = gig.TotalMembers,
                        Action = "CREATE",
                        CreatedBy = currentUser,
                        CreatedAt = DateTime.Now
                    };

                    var blockchainTx = await _blockchainService.CreateAndAddTransactionAsync(
                        "GIG_CREATE",
                        gig.GigCode,
                        companyCode,
                        0,
                        gig.Id.ToString(),
                        blockchainData);

                    if (blockchainTx != null)
                    {
                        blockchainTxId = blockchainTx.TransactionId;
                        gig.BlockchainTxId = blockchainTxId;
                        await _context.SaveChangesAsync();
                        _logger.LogInformation($"Blockchain transaction recorded for GIG {gig.GigCode}: {blockchainTxId}");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Failed to record blockchain transaction for GIG {gig.GigCode} - continuing with creation");
                }

                await transaction.CommitAsync();

                _logger.LogInformation($"GIG {gig.GigCode} created successfully");
                return MapToResponseDTO(gig, companyName, blockchainTxId);
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        // ============================================================
        // UPDATE
        // ============================================================
        public async Task<GIGsResponseDTO> UpdateGIGAsync(int id, GIGsDTO gigDto)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var companyCode = gigDto.CompanyCode?.Trim();
                var currentUser = _companyContextService.GetCurrentUserName() ?? "SYSTEM";

                if (string.IsNullOrEmpty(companyCode))
                    throw new InvalidOperationException("Company Code is required.");

                if (!IsKycCompany(companyCode))
                    throw new InvalidOperationException(
                        "Only companies with a KYC company code are allowed.");

                _logger.LogInformation($"Updating GIG ID: {id} for company: {companyCode}");

                var gig = await _context.CIGs
                    .FirstOrDefaultAsync(g => g.Id == id && g.CompanyCode == companyCode);

                if (gig == null)
                    throw new InvalidOperationException($"GIG with ID {id} not found for this company.");

                var oldValues = new
                {
                    gig.GigCode,
                    gig.GigName,
                    gig.ContactPhone,
                    gig.ContactEmail,
                    gig.Chairperson,
                    gig.TotalMembers,
                    gig.Status
                };

                var company = await _context.Companies
                    .FirstOrDefaultAsync(c => c.CompanyCode == companyCode);
                var companyName = company?.CompanyName;

                gig.GigCode = gigDto.GigCode;
                gig.GigName = gigDto.GigName;
                gig.ContactPhone = gigDto.ContactPhone;
                gig.ContactEmail = gigDto.ContactEmail;
                gig.Chairperson = gigDto.Chairperson;
                gig.RegistrationDate = gigDto.RegistrationDate ?? gig.RegistrationDate;
                gig.TotalMembers = gigDto.TotalMembers ?? gig.TotalMembers;
                gig.Status = gigDto.Status ?? gig.Status;
                gig.ModifiedBy = currentUser;
                gig.ModifiedAt = DateTime.Now;

                await _context.SaveChangesAsync();

                string blockchainTxId = null;
                try
                {
                    var blockchainData = new
                    {
                        GigId = gig.Id,
                        Action = "UPDATE",
                        OldValues = oldValues,
                        NewValues = new
                        {
                            gig.GigCode,
                            gig.GigName,
                            gig.ContactPhone,
                            gig.ContactEmail,
                            gig.Chairperson,
                            gig.TotalMembers,
                            gig.Status
                        },
                        ModifiedBy = currentUser,
                        ModifiedAt = DateTime.Now
                    };

                    var blockchainTx = await _blockchainService.CreateAndAddTransactionAsync(
                        "GIG_UPDATE",
                        gig.GigCode,
                        companyCode,
                        0,
                        gig.Id.ToString(),
                        blockchainData);

                    if (blockchainTx != null)
                    {
                        blockchainTxId = blockchainTx.TransactionId;
                        gig.BlockchainTxId = blockchainTxId;
                        await _context.SaveChangesAsync();
                        _logger.LogInformation($"Blockchain transaction recorded for GIG update: {blockchainTxId}");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Failed to record blockchain transaction for GIG update");
                }

                await transaction.CommitAsync();

                _logger.LogInformation($"GIG {gig.GigCode} updated successfully");
                return MapToResponseDTO(gig, companyName, blockchainTxId);
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        // ============================================================
        // DELETE
        // Any KYC company's GIG can be deleted (no per-user filter).
        // ============================================================
        public async Task<bool> DeleteGIGAsync(int id)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var currentUser = _companyContextService.GetCurrentUserName() ?? "SYSTEM";

                var gig = await _context.CIGs
                    .FirstOrDefaultAsync(g => g.Id == id);

                if (gig == null)
                    throw new InvalidOperationException($"GIG with ID {id} not found.");

                if (!IsKycCompany(gig.CompanyCode))
                    throw new InvalidOperationException("Only KYC company GIGs can be deleted.");

                var gigDetails = new
                {
                    gig.Id,
                    gig.GigCode,
                    gig.GigName,
                    gig.CompanyCode,
                    gig.ContactPhone,
                    gig.ContactEmail,
                    gig.Chairperson
                };

                _context.CIGs.Remove(gig);
                await _context.SaveChangesAsync();

                try
                {
                    var blockchainData = new
                    {
                        Action = "DELETE",
                        GIGDetails = gigDetails,
                        DeletedBy = currentUser,
                        DeletedAt = DateTime.Now
                    };

                    var blockchainTx = await _blockchainService.CreateAndAddTransactionAsync(
                        "GIG_DELETE",
                        gig.GigCode,
                        gig.CompanyCode,
                        0,
                        gig.Id.ToString(),
                        blockchainData);

                    _logger.LogInformation($"Blockchain transaction recorded for GIG deletion: {blockchainTx?.TransactionId}");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Failed to record blockchain transaction for GIG deletion");
                }

                await transaction.CommitAsync();

                _logger.LogInformation($"GIG {gig.GigCode} deleted successfully");
                return true;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        // ============================================================
        // GET BY ID
        // Any KYC company's GIG can be viewed.
        // ============================================================
        public async Task<GIGsResponseDTO> GetGIGByIdAsync(int id)
        {
            var gig = await _context.CIGs
                .Include(g => g.Company)
                .FirstOrDefaultAsync(g => g.Id == id);

            if (gig == null) return null;
            if (!IsKycCompany(gig.CompanyCode)) return null;

            return MapToResponseDTO(gig, gig.Company?.CompanyName, gig.BlockchainTxId);
        }

        // ============================================================
        // GET BY CODE
        // Any KYC company's GIG can be looked up by code.
        // ============================================================
        public async Task<GIGsResponseDTO> GetGIGByCodeAsync(string gigCode)
        {
            var gig = await _context.CIGs
                .Include(g => g.Company)
                .FirstOrDefaultAsync(g => g.GigCode == gigCode);

            if (gig == null) return null;
            if (!IsKycCompany(gig.CompanyCode)) return null;

            return MapToResponseDTO(gig, gig.Company?.CompanyName, gig.BlockchainTxId);
        }

        // ============================================================
        // GET ALL — returns every GIG whose CompanyCode starts with "KYC".
        // NOT filtered by the logged-in user's company.
        // ============================================================
        public async Task<List<GIGsResponseDTO>> GetAllGIGsAsync(string search = null)
        {
            var query = _context.CIGs
                .Include(g => g.Company)
                .Where(g => g.CompanyCode.StartsWith(KycCompanyPrefix))
                .AsQueryable();

            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(g =>
                    g.GigCode.Contains(search) ||
                    (g.GigName != null && g.GigName.Contains(search)) ||
                    (g.ContactPhone != null && g.ContactPhone.Contains(search)) ||
                    (g.ContactEmail != null && g.ContactEmail.Contains(search)) ||
                    (g.Chairperson != null && g.Chairperson.Contains(search)) ||
                    (g.CompanyCode != null && g.CompanyCode.Contains(search)));
            }

            var gigs = await query
                .OrderBy(g => g.CompanyCode)
                .ThenBy(g => g.GigName)
                .ToListAsync();

            return gigs.Select(g => MapToResponseDTO(g, g.Company?.CompanyName, g.BlockchainTxId)).ToList();
        }

        // ============================================================
        // GET BY COMPANY (explicit)
        // ============================================================
        public async Task<List<GIGsResponseDTO>> GetGIGsByCompanyAsync(string companyCode)
        {
            var gigs = await _context.CIGs
                .Include(g => g.Company)
                .Where(g => g.CompanyCode == companyCode)
                .OrderBy(g => g.GigName)
                .ToListAsync();

            return gigs.Select(g => MapToResponseDTO(g, g.Company?.CompanyName, g.BlockchainTxId)).ToList();
        }

        // ============================================================
        // GENERATE GIG CODE
        // ============================================================
        public async Task<string> GenerateGIGCodeAsync(string companyCode)
        {
            var prefix = "CIG";
            var date = DateTime.Now.ToString("ddHHmmssfff");

            var newCode = $"{prefix}{date}";

            var exists = await _context.CIGs
                .AnyAsync(g => g.GigCode == newCode && g.CompanyCode == companyCode);

            if (exists)
            {
                await Task.Delay(1);
                return await GenerateGIGCodeAsync(companyCode);
            }

            return newCode;
        }

        // ============================================================
        // IS GIG CODE UNIQUE (per company)
        // ============================================================
        public async Task<bool> IsGIGCodeUniqueAsync(string gigCode, string companyCode, int? excludeId = null)
        {
            var query = _context.CIGs.Where(g => g.GigCode == gigCode && g.CompanyCode == companyCode);

            if (excludeId.HasValue)
            {
                query = query.Where(g => g.Id != excludeId.Value);
            }

            return !await query.AnyAsync();
        }

        // ============================================================
        // MAPPER
        // ============================================================
        private GIGsResponseDTO MapToResponseDTO(GIGs gig, string companyName, string blockchainTxId)
        {
            return new GIGsResponseDTO
            {
                Id = gig.Id,
                GigCode = gig.GigCode,
                GigName = gig.GigName,
                CompanyCode = gig.CompanyCode,
                CompanyName = companyName,
                ContactPhone = gig.ContactPhone,
                ContactEmail = gig.ContactEmail,
                Chairperson = gig.Chairperson,
                RegistrationDate = gig.RegistrationDate,
                TotalMembers = gig.TotalMembers,
                Status = gig.Status,
                CreatedBy = gig.CreatedBy,
                CreatedAt = gig.CreatedAt,
                ModifiedBy = gig.ModifiedBy,
                ModifiedAt = gig.ModifiedAt,
                BlockchainTxId = blockchainTxId
            };
        }
    }
}