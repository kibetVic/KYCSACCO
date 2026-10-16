using Microsoft.EntityFrameworkCore;
using KYCSACCO.Data;
using KYCSACCO.Models;
using KYCSACCO.Models.DTOs;

// Ensure the same DTO used by the view / GL Setup is returned by this service
using KycCompanyLookupDTO = KYCSACCO.Services.KycCompanyLookupDTO;

namespace KYCSACCO.Services
{
    public interface IBankService
    {
        Task<BankResponseDTO> CreateBankAsync(BankDTO bankDto);
        Task<BankResponseDTO> UpdateBankAsync(int id, BankDTO bankDto);
        Task<bool> DeleteBankAsync(int id);
        Task<bool> ToggleBankStatusAsync(int id);
        Task<BankResponseDTO> GetBankByIdAsync(int id);
        Task<List<BankResponseDTO>> GetAllBanksAsync(string search = null, bool showInactive = false, string companyCode = null);
        Task<BankResponseDTO> GetBankByCodeAsync(string bankCode, string companyCode);

        // GL accounts for the selected company
        Task<List<GlAccountDropdownDTO>> GetGlAccountsForDropdownAsync(string companyCode = null);

        // KYC companies for the Bank form dropdown
        Task<List<KycCompanyLookupDTO>> GetKycCompaniesAsync();
    }

    public class BankService : IBankService
    {
        // Same prefix used by GIGsService / GlAccountService
        private const string KycPrefix = "KYC";

        private readonly ApplicationDbContext _context;
        private readonly IBlockchainService _blockchainService;
        private readonly ILogger<BankService> _logger;
        private readonly ICompanyContextService _companyContextService;

        public BankService(
            ApplicationDbContext context,
            IBlockchainService blockchainService,
            ILogger<BankService> logger,
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
                && companyCode.Trim().StartsWith(KycPrefix, StringComparison.OrdinalIgnoreCase);
        }

        // ============================================================
        // KYC COMPANIES FOR DROPDOWN
        // ============================================================
        public async Task<List<KycCompanyLookupDTO>> GetKycCompaniesAsync()
        {
            return await _context.Companies
                .AsNoTracking()
                .Where(c => c.CompanyCode != null && c.CompanyCode.StartsWith(KycPrefix))
                .OrderBy(c => c.CompanyName)
                .Select(c => new KycCompanyLookupDTO
                {
                    CompanyCode = c.CompanyCode,
                    CompanyName = c.CompanyName
                })
                .ToListAsync();
        }

        // ============================================================
        // GL ACCOUNTS — filtered by the resolved company code.
        // Returns an empty list for non-KYC companies.
        // ============================================================
        public async Task<List<GlAccountDropdownDTO>> GetGlAccountsForDropdownAsync(string companyCode = null)
        {
            if (string.IsNullOrWhiteSpace(companyCode))
                companyCode = _companyContextService.GetCurrentCompanyCode();

            if (!IsKycCompany(companyCode))
            {
                return new List<GlAccountDropdownDTO>();
            }

            return await _context.GlSetup
                .Where(g => g.CompanyCode == companyCode && g.Status == true)
                .OrderBy(g => g.AccNo)
                .Select(g => new GlAccountDropdownDTO
                {
                    AccNo = g.AccNo,
                    Glaccname = g.Glaccname ?? "",
                    Glacctype = g.Glacctype
                })
                .ToListAsync();
        }

        // ============================================================
        // CREATE
        // ============================================================
        public async Task<BankResponseDTO> CreateBankAsync(BankDTO bankDto)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var companyCode = bankDto.CompanyCode?.Trim();
                var currentUser = _companyContextService.GetCurrentUserName() ?? "SYSTEM";

                _logger.LogInformation($"Creating new bank: {bankDto.BankCode} - {bankDto.BankName} for company {companyCode}");

                if (string.IsNullOrWhiteSpace(companyCode))
                    throw new InvalidOperationException("Company Code is required.");

                if (!IsKycCompany(companyCode))
                    throw new InvalidOperationException("Only KYC companies are allowed.");

                var company = await _context.Companies
                    .FirstOrDefaultAsync(c => c.CompanyCode == companyCode);
                if (company == null)
                    throw new InvalidOperationException($"Company '{companyCode}' not found.");

                if (await _context.Banks.AnyAsync(b => b.BankCode == bankDto.BankCode && b.CompanyCode == companyCode))
                    throw new InvalidOperationException($"Bank with code {bankDto.BankCode} already exists for {companyCode}.");

                if (await _context.Banks.AnyAsync(b => b.AccountNumber == bankDto.AccountNumber && b.CompanyCode == companyCode))
                    throw new InvalidOperationException($"Account number {bankDto.AccountNumber} is already registered for {companyCode}.");

                string glAccountName = null;
                if (!string.IsNullOrEmpty(bankDto.GlAccountNo))
                {
                    var glAccount = await _context.GlSetup
                        .FirstOrDefaultAsync(g => g.AccNo == bankDto.GlAccountNo && g.CompanyCode == companyCode);
                    glAccountName = glAccount?.Glaccname;
                }

                var bank = new Bank
                {
                    BankCode = bankDto.BankCode?.Trim() ?? "",
                    BankName = bankDto.BankName?.Trim() ?? "",
                    AccountNumber = bankDto.AccountNumber?.Trim() ?? "",
                    AccountName = NormalizeOptional(bankDto.AccountName),
                    Branch = NormalizeOptional(bankDto.Branch),
                    SwiftCode = NormalizeOptional(bankDto.SwiftCode),
                    SortCode = NormalizeOptional(bankDto.SortCode),
                    GlAccountNo = NormalizeOptional(bankDto.GlAccountNo),
                    GlAccountName = glAccountName,
                    CompanyCode = companyCode,
                    IsActive = bankDto.IsActive,
                    Notes = NormalizeOptional(bankDto.Notes),
                    CreatedBy = currentUser,
                    CreatedAt = DateTime.Now
                };

                _context.Banks.Add(bank);
                await _context.SaveChangesAsync();

                string blockchainTxId = null;
                try
                {
                    var blockchainTx = await _blockchainService.CreateAndAddTransactionAsync(
                        "BANK_CREATE",
                        bank.BankCode,
                        bank.CompanyCode,
                        0,
                        bank.Id.ToString(),
                        new
                        {
                            BankId = bank.Id,
                            BankCode = bank.BankCode,
                            BankName = bank.BankName,
                            AccountNumber = bank.AccountNumber,
                            CompanyCode = bank.CompanyCode,
                            GlAccountNo = bank.GlAccountNo,
                            Action = "CREATE",
                            CreatedBy = bank.CreatedBy,
                            CreatedAt = bank.CreatedAt
                        });

                    if (blockchainTx != null)
                    {
                        blockchainTxId = blockchainTx.TransactionId;
                        bank.BlockchainTxId = blockchainTxId;
                        await _context.SaveChangesAsync();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Blockchain transaction failed for bank {bank.BankCode}");
                }

                await transaction.CommitAsync();

                return MapToResponseDTO(bank, company.CompanyName, blockchainTxId);
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
        public async Task<BankResponseDTO> UpdateBankAsync(int id, BankDTO bankDto)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var bank = await _context.Banks.FindAsync(id);
                if (bank == null)
                    throw new InvalidOperationException($"Bank with ID {id} not found.");

                var currentUser = _companyContextService.GetCurrentUserName() ?? "SYSTEM";
                var companyCode = bankDto.CompanyCode?.Trim();

                if (string.IsNullOrWhiteSpace(companyCode))
                    throw new InvalidOperationException("Company Code is required.");

                if (!IsKycCompany(companyCode))
                    throw new InvalidOperationException("Only KYC companies are allowed.");

                var company = await _context.Companies
                    .FirstOrDefaultAsync(c => c.CompanyCode == companyCode);
                if (company == null)
                    throw new InvalidOperationException($"Company '{companyCode}' not found.");

                var oldValues = new
                {
                    bank.BankCode,
                    bank.BankName,
                    bank.AccountNumber,
                    bank.AccountName,
                    bank.Branch,
                    bank.SwiftCode,
                    bank.SortCode,
                    bank.GlAccountNo,
                    bank.GlAccountName,
                    bank.CompanyCode,
                    bank.IsActive
                };

                string glAccountName = null;
                if (!string.IsNullOrEmpty(bankDto.GlAccountNo))
                {
                    var glAccount = await _context.GlSetup
                        .FirstOrDefaultAsync(g => g.AccNo == bankDto.GlAccountNo && g.CompanyCode == companyCode);
                    glAccountName = glAccount?.Glaccname;
                }

                bank.BankCode = bankDto.BankCode?.Trim() ?? "";
                bank.BankName = bankDto.BankName?.Trim() ?? "";
                bank.AccountNumber = bankDto.AccountNumber?.Trim() ?? "";
                bank.AccountName = NormalizeOptional(bankDto.AccountName);
                bank.Branch = NormalizeOptional(bankDto.Branch);
                bank.SwiftCode = NormalizeOptional(bankDto.SwiftCode);
                bank.SortCode = NormalizeOptional(bankDto.SortCode);
                bank.GlAccountNo = NormalizeOptional(bankDto.GlAccountNo);
                bank.GlAccountName = glAccountName;
                bank.CompanyCode = companyCode;
                bank.IsActive = bankDto.IsActive;
                bank.Notes = NormalizeOptional(bankDto.Notes);
                bank.ModifiedBy = currentUser;
                bank.ModifiedAt = DateTime.Now;

                await _context.SaveChangesAsync();

                string blockchainTxId = null;
                try
                {
                    var blockchainTx = await _blockchainService.CreateAndAddTransactionAsync(
                        "BANK_UPDATE",
                        bank.BankCode,
                        bank.CompanyCode,
                        0,
                        bank.Id.ToString(),
                        new
                        {
                            BankId = bank.Id,
                            Action = "UPDATE",
                            OldValues = oldValues,
                            NewValues = new
                            {
                                bank.BankCode,
                                bank.BankName,
                                bank.AccountNumber,
                                bank.AccountName,
                                bank.Branch,
                                bank.SwiftCode,
                                bank.SortCode,
                                bank.GlAccountNo,
                                bank.GlAccountName,
                                bank.CompanyCode,
                                bank.IsActive
                            },
                            ModifiedBy = bank.ModifiedBy,
                            ModifiedAt = bank.ModifiedAt
                        });

                    if (blockchainTx != null)
                    {
                        blockchainTxId = blockchainTx.TransactionId;
                        bank.BlockchainTxId = blockchainTxId;
                        await _context.SaveChangesAsync();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Blockchain transaction failed for bank update");
                }

                await transaction.CommitAsync();

                return MapToResponseDTO(bank, company.CompanyName, blockchainTxId);
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        // ============================================================
        // DELETE
        // ============================================================
        public async Task<bool> DeleteBankAsync(int id)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var bank = await _context.Banks.FindAsync(id);
                if (bank == null)
                    throw new InvalidOperationException($"Bank with ID {id} not found.");

                var currentUser = _companyContextService.GetCurrentUserName() ?? "SYSTEM";

                var bankDetails = new
                {
                    bank.Id,
                    bank.BankCode,
                    bank.BankName,
                    bank.AccountNumber,
                    bank.CompanyCode
                };

                _context.Banks.Remove(bank);
                await _context.SaveChangesAsync();

                try
                {
                    var blockchainTx = await _blockchainService.CreateAndAddTransactionAsync(
                        "BANK_DELETE",
                        bank.BankCode,
                        bank.CompanyCode,
                        0,
                        bank.Id.ToString(),
                        new
                        {
                            Action = "DELETE",
                            BankDetails = bankDetails,
                            DeletedBy = currentUser,
                            DeletedAt = DateTime.Now
                        });

                    _logger.LogInformation($"Blockchain tx for bank delete: {blockchainTx?.TransactionId}");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Blockchain tx failed for bank delete");
                }

                await transaction.CommitAsync();
                return true;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        // ============================================================
        // TOGGLE STATUS
        // ============================================================
        public async Task<bool> ToggleBankStatusAsync(int id)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var bank = await _context.Banks.FindAsync(id);
                if (bank == null)
                    throw new InvalidOperationException($"Bank with ID {id} not found.");

                var currentUser = _companyContextService.GetCurrentUserName() ?? "SYSTEM";
                var oldStatus = bank.IsActive;
                bank.IsActive = !bank.IsActive;
                bank.ModifiedBy = currentUser;
                bank.ModifiedAt = DateTime.Now;

                await _context.SaveChangesAsync();

                try
                {
                    var blockchainTx = await _blockchainService.CreateAndAddTransactionAsync(
                        "BANK_STATUS_TOGGLE",
                        bank.BankCode,
                        bank.CompanyCode,
                        0,
                        bank.Id.ToString(),
                        new
                        {
                            BankId = bank.Id,
                            BankCode = bank.BankCode,
                            Action = "TOGGLE_STATUS",
                            OldStatus = oldStatus,
                            NewStatus = bank.IsActive,
                            ModifiedBy = bank.ModifiedBy,
                            ModifiedAt = bank.ModifiedAt
                        });

                    if (blockchainTx != null)
                    {
                        bank.BlockchainTxId = blockchainTx.TransactionId;
                        await _context.SaveChangesAsync();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Blockchain tx failed for bank status toggle");
                }

                await transaction.CommitAsync();
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
        // ============================================================
        public async Task<BankResponseDTO> GetBankByIdAsync(int id)
        {
            var bank = await _context.Banks.FindAsync(id);
            if (bank == null) return null;

            var companyName = await _context.Companies
                .Where(c => c.CompanyCode == bank.CompanyCode)
                .Select(c => c.CompanyName)
                .FirstOrDefaultAsync();

            return MapToResponseDTO(bank, companyName, bank.BlockchainTxId);
        }

        // ============================================================
        // GET ALL — every KYC bank, optional company filter
        // ============================================================
        public async Task<List<BankResponseDTO>> GetAllBanksAsync(
            string search = null,
            bool showInactive = false,
            string companyCode = null)
        {
            var query = _context.Banks
                .Where(b => b.CompanyCode.StartsWith(KycPrefix))
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(companyCode))
            {
                query = query.Where(b => b.CompanyCode == companyCode);
            }

            if (!showInactive)
                query = query.Where(b => b.IsActive);

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(b =>
                    b.BankCode.Contains(search) ||
                    b.BankName.Contains(search) ||
                    b.AccountNumber.Contains(search) ||
                    (b.Branch != null && b.Branch.Contains(search)) ||
                    (b.AccountName != null && b.AccountName.Contains(search)) ||
                    (b.GlAccountNo != null && b.GlAccountNo.Contains(search)) ||
                    b.CompanyCode.Contains(search));
            }

            var banks = await query
                .OrderBy(b => b.CompanyCode)
                .ThenBy(b => b.BankName)
                .ToListAsync();

            var companyCodes = banks.Select(b => b.CompanyCode).Distinct().ToList();
            var companyNames = await _context.Companies
                .Where(c => companyCodes.Contains(c.CompanyCode))
                .ToDictionaryAsync(c => c.CompanyCode, c => c.CompanyName);

            return banks
                .Select(b => MapToResponseDTO(b, companyNames.GetValueOrDefault(b.CompanyCode), b.BlockchainTxId))
                .ToList();
        }

        // ============================================================
        // GET BY CODE
        // ============================================================
        public async Task<BankResponseDTO> GetBankByCodeAsync(string bankCode, string companyCode)
        {
            if (!IsKycCompany(companyCode))
                return null;

            var bank = await _context.Banks
                .FirstOrDefaultAsync(b => b.BankCode == bankCode && b.CompanyCode == companyCode);

            if (bank == null) return null;

            var companyName = await _context.Companies
                .Where(c => c.CompanyCode == bank.CompanyCode)
                .Select(c => c.CompanyName)
                .FirstOrDefaultAsync();

            return MapToResponseDTO(bank, companyName, bank.BlockchainTxId);
        }

        // ============================================================
        // MAPPERS
        // ============================================================
        private static string? NormalizeOptional(string? value)
            => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        private BankResponseDTO MapToResponseDTO(Bank bank, string companyName, string blockchainTxId)
        {
            return new BankResponseDTO
            {
                Id = bank.Id,
                BankCode = bank.BankCode,
                BankName = bank.BankName,
                AccountNumber = bank.AccountNumber,
                AccountName = bank.AccountName,
                Branch = bank.Branch,
                SwiftCode = bank.SwiftCode,
                SortCode = bank.SortCode,
                GlAccountNo = bank.GlAccountNo,
                GlAccountName = bank.GlAccountName,
                CompanyCode = bank.CompanyCode,
                CompanyName = companyName,
                IsActive = bank.IsActive,
                Notes = bank.Notes,
                CreatedBy = bank.CreatedBy,
                CreatedAt = bank.CreatedAt,
                ModifiedBy = bank.ModifiedBy,
                ModifiedAt = bank.ModifiedAt,
                BlockchainTxId = blockchainTxId
            };
        }
    }
}