using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KYCSACCO.Models.DTOs;
using KYCSACCO.Services;

// Same DTO as the one returned by BankService.GetKycCompaniesAsync()
using KycCompanyLookupDTO = KYCSACCO.Services.KycCompanyLookupDTO;

namespace KYCSACCO.Controllers
{
    [Authorize]
    public class BankController : Controller
    {
        private const string KycPrefix = "KYC";

        private readonly IBankService _bankService;
        private readonly ILogger<BankController> _logger;

        public BankController(IBankService bankService, ILogger<BankController> logger)
        {
            _bankService = bankService;
            _logger = logger;
        }

        // ============================================================
        // KYC validator
        // ============================================================
        private static string ValidateKycCompanyCode(string postedCompanyCode)
        {
            if (string.IsNullOrWhiteSpace(postedCompanyCode)) return null;
            var trimmed = postedCompanyCode.Trim();
            return trimmed.StartsWith(KycPrefix, StringComparison.OrdinalIgnoreCase)
                ? trimmed
                : null;
        }

        // ============================================================
        // INDEX
        // ============================================================
        public async Task<IActionResult> Index(string search, bool showInactive = false, string companyCode = null)
        {
            try
            {
                var validatedCompanyCode = ValidateKycCompanyCode(companyCode);
                if (validatedCompanyCode == null) companyCode = null;

                ViewBag.CurrentSearch = search;
                ViewBag.ShowInactive = showInactive;
                ViewBag.SelectedCompanyCode = companyCode;

                // KYC companies (same DTO the view expects)
                ViewBag.Companies = await _bankService.GetKycCompaniesAsync();

                var banks = await _bankService.GetAllBanksAsync(search, showInactive, companyCode);
                return View(banks);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading banks");
                TempData["ErrorMessage"] = "Error loading banks. Please try again.";
                ViewBag.Companies = new List<KycCompanyLookupDTO>();
                return View(new List<BankResponseDTO>());
            }
        }

        // ============================================================
        // GET KYC COMPANIES
        // ============================================================
        [HttpGet]
        public async Task<IActionResult> GetKycCompanies()
        {
            try
            {
                var companies = await _bankService.GetKycCompaniesAsync();
                return Json(new { success = true, companies });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting KYC companies");
                return Json(new { success = false, message = ex.Message });
            }
        }

        // ============================================================
        // GET GL ACCOUNTS
        // ============================================================
        [HttpGet]
        public async Task<IActionResult> GetGlAccounts(string companyCode = null)
        {
            try
            {
                var validated = ValidateKycCompanyCode(companyCode);
                if (validated == null)
                {
                    return Json(new { success = true, data = new List<GlAccountDropdownDTO>() });
                }

                var glAccounts = await _bankService.GetGlAccountsForDropdownAsync(validated);
                return Json(new { success = true, data = glAccounts });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting GL accounts");
                return Json(new { success = false, message = ex.Message });
            }
        }

        // ============================================================
        // CREATE
        // ============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([FromForm] BankDTO bankDto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                    return Json(new { success = false, message = string.Join(", ", errors) });
                }

                if (string.IsNullOrWhiteSpace(bankDto.CompanyCode))
                    return Json(new { success = false, message = "Please select a Company." });

                if (ValidateKycCompanyCode(bankDto.CompanyCode) == null)
                    return Json(new { success = false, message = "Only KYC companies are allowed." });

                bankDto.CompanyCode = bankDto.CompanyCode.Trim();

                var bank = await _bankService.CreateBankAsync(bankDto);
                return Json(new { success = true, message = $"Bank {bank.BankCode} created successfully.", bank });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating bank");
                return Json(new { success = false, message = ex.Message });
            }
        }

        // ============================================================
        // EDIT
        // ============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit([FromForm] BankDTO bankDto, int id)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                    return Json(new { success = false, message = string.Join(", ", errors) });
                }

                if (string.IsNullOrWhiteSpace(bankDto.CompanyCode))
                    return Json(new { success = false, message = "Please select a Company." });

                if (ValidateKycCompanyCode(bankDto.CompanyCode) == null)
                    return Json(new { success = false, message = "Only KYC companies are allowed." });

                bankDto.CompanyCode = bankDto.CompanyCode.Trim();

                var bank = await _bankService.UpdateBankAsync(id, bankDto);
                return Json(new { success = true, message = $"Bank {bank.BankCode} updated successfully.", bank });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating bank");
                return Json(new { success = false, message = ex.Message });
            }
        }

        // ============================================================
        // GET DETAILS
        // ============================================================
        [HttpGet]
        public async Task<IActionResult> GetBankDetails(int id)
        {
            try
            {
                var bank = await _bankService.GetBankByIdAsync(id);
                if (bank == null)
                    return Json(new { success = false, message = "Bank not found." });

                return Json(new { success = true, bank });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting bank details");
                return Json(new { success = false, message = ex.Message });
            }
        }

        // ============================================================
        // TOGGLE STATUS
        // ============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleStatus(int id)
        {
            try
            {
                await _bankService.ToggleBankStatusAsync(id);
                return Json(new { success = true, message = "Bank status updated successfully." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error toggling bank status");
                return Json(new { success = false, message = ex.Message });
            }
        }

        // ============================================================
        // DELETE
        // ============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                await _bankService.DeleteBankAsync(id);
                return Json(new { success = true, message = "Bank deleted successfully." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting bank");
                return Json(new { success = false, message = ex.Message });
            }
        }
    }
}