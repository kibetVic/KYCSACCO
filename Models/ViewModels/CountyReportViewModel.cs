using System;
using System.Collections.Generic;

namespace KYCSACCO.Models.ViewModels
{
    // ============================================================
    // CONTRIBUTION REPORT
    // ============================================================
    public class CountyReportViewModel
    {
        public string CountyName { get; set; }
        public DateTime ReportDate { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public int TotalCompanies { get; set; }
        public int TotalMembers { get; set; }
        public decimal TotalContributions { get; set; }
        public decimal TotalShareCapital { get; set; }
        public decimal TotalDeposits { get; set; }
        public decimal TotalRegistrationFees { get; set; }
        public List<CompanyContributionSummary> CompanySummaries { get; set; } = new List<CompanyContributionSummary>();
        public GenderContributionSummary GenderSummary { get; set; } = new GenderContributionSummary();
        public bool IsLoading { get; set; } = false;
        public string CacheKey { get; set; }

        // For layout display
        public bool IsCountyView { get; set; } = true;
        public List<CompanyInfo> Companies { get; set; } = new List<CompanyInfo>();
        public string SelectedCompanyName { get; set; }
        public string SelectedCompanyCode { get; set; }
        public string UserGroup { get; set; }
        public List<string> UserRoles { get; set; } = new List<string>();
    }

    public class CompanyContributionSummary
    {
        public string CompanyCode { get; set; }
        public string CompanyName { get; set; }
        public int MemberCount { get; set; }
        public decimal TotalContributions { get; set; }
        public decimal TotalShareCapital { get; set; }
        public decimal TotalDeposits { get; set; }
        public decimal TotalRegistrationFees { get; set; }
        public decimal AverageContributionPerMember { get; set; }
        public decimal FemaleContributions { get; set; }
        public decimal MaleContributions { get; set; }
        public decimal OtherContributions { get; set; }
        public int FemaleCount { get; set; }
        public int MaleCount { get; set; }
        public int OtherCount { get; set; }
        public decimal PercentageOfTotal { get; set; }
        public decimal WomenPercentage { get; set; }
        public decimal MenPercentage { get; set; }
        public decimal OtherPercentage { get; set; }
    }

    public class GenderContributionSummary
    {
        public decimal FemaleTotal { get; set; }
        public decimal MaleTotal { get; set; }
        public decimal OtherTotal { get; set; }
        public int FemaleCount { get; set; }
        public int MaleCount { get; set; }
        public int OtherCount { get; set; }
        public decimal FemalePercentage { get; set; }
        public decimal MalePercentage { get; set; }
        public decimal OtherPercentage { get; set; }
    }



    // ============================================================
    // LOANS REPORT
    // ============================================================
    public class CountyLoansReportViewModel
    {
        public string CountyName { get; set; }
        public DateTime ReportDate { get; set; }
        public DateTime? AsAtDate { get; set; }
        public int TotalCompanies { get; set; }
        public int TotalLoans { get; set; }
        public decimal TotalLoanAmount { get; set; }
        public decimal TotalLoanBalance { get; set; }
        public decimal TotalLoansPaid { get; set; }  // NEW
        public decimal OverallAmountPastDueRate { get; set; }
        public decimal OverallPAR30 { get; set; }
        public decimal OverallPAR60 { get; set; }
        public decimal OverallPAR90 { get; set; }  // NEW
        public List<SaccoLoanSummary> SaccoSummaries { get; set; } = new List<SaccoLoanSummary>();
        public bool IsLoading { get; set; } = false;
        public string CacheKey { get; set; }
        public bool IsCountyView { get; set; } = false;
        public string SelectedCompanyName { get; set; }
        public string SelectedCompanyCode { get; set; }
        public List<CompanyInfo> Companies { get; set; } = new List<CompanyInfo>();
        public string UserGroup { get; set; }
        public List<string> UserRoles { get; set; } = new List<string>();
        public int TotalLoanees { get; internal set; }
        public decimal TotalInterestFromLoans { get; internal set; }
        public decimal TotalPenaltyAmount { get; internal set; }
    }

    public class SaccoLoanSummary
    {
        public string CompanyCode { get; set; }
        public string CompanyName { get; set; }
        public int NumberOfLoans { get; set; }
        public decimal TotalLoanAmount { get; set; }
        public decimal TotalLoanBalance { get; set; }
        public decimal AmountPastDueRate { get; set; }
        public decimal PAR30 { get; set; }
        public decimal PAR60 { get; set; }
        public decimal PAR90 { get; set; }  // NEW
        public int LoansInArrears30 { get; set; }
        public int LoansInArrears60 { get; set; }
        public int LoansInArrears90 { get; set; }  // NEW
        public decimal TotalArrears30 { get; set; }
        public decimal TotalArrears60 { get; set; }
        public decimal TotalArrears90 { get; set; }  // NEW
        public decimal TotalLoansPaid { get; set; }  // NEW
        public decimal PercentageOfTotal { get; set; }
        public string PortfolioHealth { get; set; }
        public int NumberOfLoanees { get; internal set; }
        public decimal TotalInterestFromLoans { get; internal set; }
        public decimal TotalPenaltyAmount { get; internal set; }
        public string LoanProductNo { get; internal set; }
        public string LoanProductName { get; internal set; }
    }

    /// <summary>
    /// County-level report showing each SACCO's performance metrics
    /// </summary>
    /// 


    public class CountySaccoReportViewModel
    {
        // Report metadata
        public string CountyName { get; set; }
        public DateTime ReportDate { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }

        // Layout properties - FIXES THE ERROR
        public bool IsCountyView { get; set; } = true;
        public string SelectedCompanyName { get; set; } = "";
        public string SelectedCompanyCode { get; set; } = "";
        public string UserGroup { get; set; } = "";
        public List<string> UserRoles { get; set; } = new List<string>();

        // ADD THIS - The missing property!
        public List<CompanyInfo> Companies { get; set; } = new List<CompanyInfo>();

        // County totals
        public int TotalCompanies { get; set; }
        public int TotalMembers { get; set; }
        public decimal TotalInclusionGrants { get; set; }
        public decimal TotalMatchingGrants { get; set; }
        public decimal TotalProfitLoss { get; set; }
        public decimal TotalAssets { get; set; }

        // Gender totals
        public int TotalMale { get; set; }
        public int TotalFemale { get; set; }
        public int TotalOthers { get; set; }
        public int TotalActive { get; set; }
        public int TotalInactive { get; set; }

        // Individual SACCO summaries
        public List<SaccoReportSummary> SaccoSummaries { get; set; } = new List<SaccoReportSummary>();
    }

    /// <summary>
    /// Individual SACCO summary for the county report
    /// </summary>
    public class SaccoReportSummary
    {
        public string CompanyCode { get; set; }
        public string CompanyName { get; set; }
        public string SubCounty { get; set; }
        public string Ward { get; set; }
        public string CSRegNO { get; set; }

        // Member counts
        public int TotalMembers { get; set; }
        public int MaleCount { get; set; }
        public int FemaleCount { get; set; }
        public int OthersCount { get; set; }
        public int ActiveCount { get; set; }
        public int InactiveCount { get; set; }

        // Financial metrics
        public decimal InclusionGrant { get; set; }
        public decimal MatchingGrant { get; set; }
        public decimal ProfitLoss { get; set; }
        public decimal TotalAssets { get; set; }

        // Percentage of total for the county
        public decimal PercentageOfTotalMembers { get; set; }
        public decimal PercentageOfTotalAssets { get; set; }
    }
}