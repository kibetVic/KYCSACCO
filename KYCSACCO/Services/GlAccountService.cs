using Microsoft.EntityFrameworkCore;
using KYCSACCO.Data;
using KYCSACCO.Models;

namespace KYCSACCO.Services
{
    public interface IGlAccountService
    {
        Task<List<GlSetup>> GetShareGlAccountsAsync(string companyCode);
        Task<GlSetup> GetGlAccountByCodeAsync(string accountCode, string companyCode);
        Task<List<KycCompanyLookupDTO>> GetKycCompaniesAsync();
    }

    public class GlAccountService : IGlAccountService
    {
        private const string KycPrefix = "KYC";

        private readonly ApplicationDbContext _context;

        public GlAccountService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<GlSetup>> GetShareGlAccountsAsync(string companyCode)
        {
            if (string.IsNullOrEmpty(companyCode))
                return new List<GlSetup>();

            return await _context.GlSetup
                .Where(g => g.CompanyCode == companyCode)
                .OrderBy(g => g.AccNo)
                .ToListAsync();
        }

        public async Task<GlSetup> GetGlAccountByCodeAsync(string accountCode, string companyCode)
        {
            return await _context.GlSetup
                .FirstOrDefaultAsync(x => x.AccNo == accountCode
                                       && x.CompanyCode == companyCode
                                       && x.Status == true);
        }

        // ============================================================
        // KYC companies for the Account Setup dropdown
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
    }

    // If you don't already have this DTO, add it (Models/DTOs/KycCompanyLookupDTO.cs)
    public class KycCompanyLookupDTO
    {
        public string CompanyCode { get; set; } = null!;
        public string CompanyName { get; set; } = null!;
        public string DisplayText => $"{CompanyCode} - {CompanyName}";
    }
}