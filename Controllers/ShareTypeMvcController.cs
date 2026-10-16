// Controllers/ShareTypeMvcController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KYCSACCO.Models.DTOs;
using KYCSACCO.Models.ViewModels;
using KYCSACCO.Services;
using System.ComponentModel.DataAnnotations;

// Same DTO the view expects
using KycCompanyLookupDTO = KYCSACCO.Services.KycCompanyLookupDTO;

namespace KYCSACCO.Controllers
{
    [Authorize]
    public class ShareTypeMvcController : Controller
    {
        private const string KycPrefix = "KYC";

        private readonly IShareTypeService _shareTypeService;
        private readonly IGlAccountService _glAccountService;
        private readonly ILogger<ShareTypeMvcController> _logger;

        public ShareTypeMvcController(
            IShareTypeService shareTypeService,
            IGlAccountService glAccountService,
            ILogger<ShareTypeMvcController> logger)
        {
            _shareTypeService = shareTypeService;
            _glAccountService = glAccountService;
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
        // GET: /ShareTypeMvc/Index
        // ============================================================
        public async Task<IActionResult> Index(string companyCode = null)
        {
            try
            {
                var effectiveCompanyCode = ValidateKycCompanyCode(companyCode)
                                           ?? GetUserCompanyCode();

                var shareTypes = await _shareTypeService.GetShareTypesByCompanyAsync(effectiveCompanyCode);

                var createDto = new ShareTypeCreateDTO
                {
                    CompanyCode = effectiveCompanyCode,
                    CreatedBy = User.Identity?.Name ?? "SYSTEM",
                    IsMainShares = false,
                    Priority = 1,
                    MinAmount = 0,
                    LowerLimit = 0,
                    ElseRatio = 0
                };

                var glAccounts = await _glAccountService.GetShareGlAccountsAsync(effectiveCompanyCode);

                var companyName = User.FindFirst("CompanyName")?.Value
                                 ?? HttpContext.Session.GetString("CompanyName")
                                 ?? "AMTECH SACCO";

                var shareTypeViewModels = shareTypes.Select(st => new ShareTypeViewModel
                {
                    SharesCode = st.SharesCode,
                    SharesType = st.SharesType,
                    SharesAcc = st.SharesAcc,
                    MinAmount = st.MinAmount,
                    MaxAmount = st.MaxAmount ?? 0,
                    LoanToShareRatio = st.LoanToShareRatio.HasValue ? (decimal)st.LoanToShareRatio.Value : 0m,
                    IsMainShares = st.IsMainShares,
                    Issharecapital = st.Issharecapital,
                    Withdrawable = st.Withdrawable,
                    UsedToOffset = st.UsedToOffset,
                    UsedToGuarantee = st.UsedToGuarantee,
                    CompanyCode = st.CompanyCode,
                    CompanyName = companyName
                }).ToList();

                ViewBag.GlAccounts = glAccounts;
                ViewBag.ShareTypes = shareTypeViewModels;
                ViewBag.CompanyName = companyName;

                ViewBag.Companies = await _shareTypeService.GetKycCompaniesAsync();
                ViewBag.SelectedCompanyCode = effectiveCompanyCode;

                return View(createDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading share types index");
                TempData["ErrorMessage"] = "Error loading share types. Please try again.";

                try
                {
                    ViewBag.Companies = await _shareTypeService.GetKycCompaniesAsync();
                }
                catch
                {
                    ViewBag.Companies = new List<KycCompanyLookupDTO>();
                }

                return View("Error");
            }
        }

        // ============================================================
        // POST: /ShareTypeMvc/Create
        // ============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ShareTypeCreateDTO shareTypeDto, string action)
        {
            try
            {
                var companyCode = ValidateKycCompanyCode(shareTypeDto.CompanyCode);
                if (string.IsNullOrEmpty(companyCode))
                {
                    TempData["ErrorMessage"] = "Please select a valid KYC company.";
                    return RedirectToAction("Index");
                }

                _logger.LogInformation($"========== CREATE POST ==========");
                _logger.LogInformation($"Received action: '{action}'");
                _logger.LogInformation($"ShareCode: {shareTypeDto.SharesCode}");
                _logger.LogInformation($"Company: {companyCode}");

                if (string.IsNullOrEmpty(action)) action = "save";

                shareTypeDto.CompanyCode = companyCode;
                shareTypeDto.CreatedBy = User.Identity?.Name ?? "SYSTEM";

                if (string.IsNullOrWhiteSpace(shareTypeDto.SharesCode))
                {
                    TempData["ErrorMessage"] = "Share Code is required";
                    return RedirectToAction("Index");
                }
                if (string.IsNullOrWhiteSpace(shareTypeDto.SharesType))
                {
                    TempData["ErrorMessage"] = "Share Type is required";
                    return RedirectToAction("Index");
                }
                if (string.IsNullOrWhiteSpace(shareTypeDto.SharesAcc))
                {
                    TempData["ErrorMessage"] = "Share Account is required";
                    return RedirectToAction("Index");
                }

                // ---------- SAVE ----------
                if (action.ToLower() == "save")
                {
                    _logger.LogInformation("Processing SAVE operation for {SharesCode}", shareTypeDto.SharesCode);

                    // Safe lookup — no exception if not found
                    var existingShareType = await _shareTypeService
                        .GetShareTypeByCodeOrNullAsync(shareTypeDto.SharesCode, companyCode);

                    if (existingShareType != null)
                    {
                        TempData["ErrorMessage"] = $"Share code '{shareTypeDto.SharesCode}' already exists. Please use a different code.";
                        return RedirectToAction("Index");
                    }

                    var result = await _shareTypeService.CreateShareTypeAsync(shareTypeDto);
                    TempData["SuccessMessage"] = $"Share type '{result.SharesType}' created successfully!";
                    return RedirectToAction("Index");
                }
                // ---------- UPDATE ----------
                else if (action.ToLower() == "update")
                {
                    _logger.LogInformation("Processing UPDATE operation for {SharesCode}", shareTypeDto.SharesCode);

                    var existingShareType = await _shareTypeService
                        .GetShareTypeByCodeOrNullAsync(shareTypeDto.SharesCode, companyCode);

                    if (existingShareType == null)
                    {
                        TempData["ErrorMessage"] = $"Share code '{shareTypeDto.SharesCode}' not found.";
                        return RedirectToAction("Index");
                    }

                    var updateDto = new ShareTypeUpdateDTO
                    {
                        SharesCode = shareTypeDto.SharesCode,
                        SharesType = shareTypeDto.SharesType,
                        SharesAcc = shareTypeDto.SharesAcc,
                        MinAmount = shareTypeDto.MinAmount,
                        MaxAmount = shareTypeDto.MaxAmount,
                        LoanToShareRatio = shareTypeDto.LoanToShareRatio,
                        IsMainShares = shareTypeDto.IsMainShares,
                        Withdrawable = shareTypeDto.Withdrawable,
                        UsedToOffset = shareTypeDto.UsedToOffset,
                        UsedToGuarantee = shareTypeDto.UsedToGuarantee,
                        Ppacc = string.IsNullOrEmpty(shareTypeDto.Ppacc) ? shareTypeDto.SharesAcc : shareTypeDto.Ppacc,
                        CompanyCode = companyCode,
                        CreatedBy = shareTypeDto.CreatedBy,
                        ContraAcc = shareTypeDto.ContraAcc,
                        PlacePeriod = shareTypeDto.PlacePeriod,
                        Issharecapital = shareTypeDto.Issharecapital,
                        Interest = shareTypeDto.Interest,
                        Guarantor = shareTypeDto.Guarantor,
                        Loanquaranto = shareTypeDto.Loanquaranto,
                        Priority = shareTypeDto.Priority,
                        LowerLimit = shareTypeDto.LowerLimit,
                        ElseRatio = shareTypeDto.ElseRatio
                    };

                    await _shareTypeService.UpdateShareTypeAsync(updateDto.SharesCode, updateDto);
                    TempData["SuccessMessage"] = $"Share type '{shareTypeDto.SharesType}' updated successfully!";
                    return RedirectToAction("Index");
                }
                // ---------- DELETE ----------
                else if (action.ToLower() == "delete")
                {
                    _logger.LogInformation("Processing DELETE operation for {SharesCode}", shareTypeDto.SharesCode);

                    var existingShareType = await _shareTypeService
                        .GetShareTypeByCodeOrNullAsync(shareTypeDto.SharesCode, companyCode);

                    if (existingShareType == null)
                    {
                        TempData["ErrorMessage"] = $"Share code '{shareTypeDto.SharesCode}' not found.";
                        return RedirectToAction("Index");
                    }

                    try
                    {
                        await _shareTypeService.DeleteShareTypeAsync(shareTypeDto.SharesCode, companyCode);
                        TempData["SuccessMessage"] = $"Share type '{shareTypeDto.SharesType}' deleted successfully!";
                        return RedirectToAction("Index");
                    }
                    catch (ValidationException ex)
                    {
                        TempData["ErrorMessage"] = ex.Message;
                        return RedirectToAction("Index");
                    }
                    catch (KeyNotFoundException)
                    {
                        TempData["ErrorMessage"] = $"Share code '{shareTypeDto.SharesCode}' not found.";
                        return RedirectToAction("Index");
                    }
                }

                TempData["ErrorMessage"] = $"Unknown action: {action}";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing share type for {SharesCode}", shareTypeDto?.SharesCode);

                var error = ex.InnerException?.InnerException?.Message
                            ?? ex.InnerException?.Message
                            ?? ex.Message;

                if (error.Contains("duplicate key") || error.Contains("PRIMARY KEY") || error.Contains("already exists"))
                    TempData["ErrorMessage"] = $"Share code '{shareTypeDto?.SharesCode}' already exists. Please use a different code.";
                else if (error.Contains("in use") || error.Contains("used by"))
                    TempData["ErrorMessage"] = error;
                else
                    TempData["ErrorMessage"] = $"An error occurred: {error}";

                return RedirectToAction("Index");
            }
        }

        // ============================================================
        // GET: /ShareTypeMvc/Edit/{sharesCode}
        // ============================================================
        public async Task<IActionResult> Edit(string sharesCode, string companyCode = null)
        {
            try
            {
                var effectiveCompanyCode = ValidateKycCompanyCode(companyCode)
                                           ?? GetUserCompanyCode();

                var shareType = await _shareTypeService.GetShareTypeByCodeAsync(sharesCode, effectiveCompanyCode);
                var glAccounts = await _glAccountService.GetShareGlAccountsAsync(effectiveCompanyCode);
                ViewBag.GlAccounts = glAccounts;

                var updateDto = new ShareTypeUpdateDTO
                {
                    SharesCode = shareType.SharesCode,
                    SharesType = shareType.SharesType,
                    SharesAcc = shareType.SharesAcc,
                    ContraAcc = shareType.ContraAcc,
                    PlacePeriod = shareType.PlacePeriod,
                    LoanToShareRatio = shareType.LoanToShareRatio,
                    Issharecapital = shareType.Issharecapital,
                    Interest = shareType.Interest,
                    MaxAmount = shareType.MaxAmount,
                    Guarantor = shareType.Guarantor,
                    IsMainShares = shareType.IsMainShares,
                    UsedToGuarantee = shareType.UsedToGuarantee,
                    UsedToOffset = shareType.UsedToOffset,
                    Withdrawable = shareType.Withdrawable,
                    Loanquaranto = shareType.Loanquaranto,
                    Priority = shareType.Priority,
                    MinAmount = shareType.MinAmount,
                    LowerLimit = shareType.LowerLimit,
                    ElseRatio = shareType.ElseRatio,
                    CompanyCode = shareType.CompanyCode,
                    CreatedBy = User.Identity?.Name ?? "SYSTEM"
                };

                var shareTypes = await _shareTypeService.GetShareTypesByCompanyAsync(effectiveCompanyCode);
                var shareTypeViewModels = shareTypes.Select(st => new ShareTypeViewModel
                {
                    SharesCode = st.SharesCode,
                    SharesType = st.SharesType,
                    SharesAcc = st.SharesAcc,
                    MinAmount = st.MinAmount,
                    MaxAmount = st.MaxAmount ?? 0,
                    LoanToShareRatio = st.LoanToShareRatio.HasValue ? (decimal)st.LoanToShareRatio.Value : 0m,
                    IsMainShares = st.IsMainShares,
                    Withdrawable = st.Withdrawable,
                    UsedToOffset = st.UsedToOffset,
                    UsedToGuarantee = st.UsedToGuarantee,
                    CompanyCode = st.CompanyCode,
                }).ToList();

                ViewBag.ShareTypes = shareTypeViewModels;
                ViewBag.CompanyName = User.FindFirst("CompanyName")?.Value ?? "AMTECH SACCO";

                ViewBag.Companies = await _shareTypeService.GetKycCompaniesAsync();
                ViewBag.SelectedCompanyCode = effectiveCompanyCode;

                return View(updateDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error loading edit form for share type {sharesCode}");
                TempData["ErrorMessage"] = $"Error loading share type {sharesCode} for editing.";
                return RedirectToAction("Index");
            }
        }

        // ============================================================
        // POST: /ShareTypeMvc/Edit
        // ============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(ShareTypeUpdateDTO updateDto)
        {
            try
            {
                var companyCode = ValidateKycCompanyCode(updateDto.CompanyCode);
                if (string.IsNullOrEmpty(companyCode))
                {
                    TempData["ErrorMessage"] = "Please select a valid KYC company.";
                    return RedirectToAction("Index");
                }

                if (!ModelState.IsValid)
                {
                    var glAccounts = await _glAccountService.GetShareGlAccountsAsync(companyCode);
                    ViewBag.GlAccounts = glAccounts;

                    var shareTypes = await _shareTypeService.GetShareTypesByCompanyAsync(companyCode);
                    var shareTypeViewModels = shareTypes.Select(st => new ShareTypeViewModel
                    {
                        SharesCode = st.SharesCode,
                        SharesType = st.SharesType,
                        SharesAcc = st.SharesAcc,
                        MinAmount = st.MinAmount,
                        MaxAmount = st.MaxAmount ?? 0,
                        LoanToShareRatio = st.LoanToShareRatio.HasValue ? (decimal)st.LoanToShareRatio.Value : 0m,
                        IsMainShares = st.IsMainShares,
                        Issharecapital = st.Issharecapital,
                        Withdrawable = st.Withdrawable,
                        UsedToOffset = st.UsedToOffset,
                        UsedToGuarantee = st.UsedToGuarantee,
                        CompanyCode = st.CompanyCode,
                    }).ToList();

                    ViewBag.ShareTypes = shareTypeViewModels;
                    ViewBag.CompanyName = User.FindFirst("CompanyName")?.Value ?? "AMTECH SACCO";

                    ViewBag.Companies = await _shareTypeService.GetKycCompaniesAsync();
                    ViewBag.SelectedCompanyCode = companyCode;

                    return View(updateDto);
                }

                updateDto.CompanyCode = companyCode;
                updateDto.CreatedBy = User.Identity?.Name ?? "SYSTEM";

                var result = await _shareTypeService.UpdateShareTypeAsync(updateDto.SharesCode, updateDto);
                TempData["SuccessMessage"] = $"Share type '{result.SharesType}' updated successfully!";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating share type {updateDto?.SharesCode}");
                TempData["ErrorMessage"] = $"Error updating share type: {ex.Message}";
                return RedirectToAction("Index");
            }
        }

        // ============================================================
        // GET: /ShareTypeMvc/Details/{sharesCode}
        // ============================================================
        public async Task<IActionResult> Details(string sharesCode, string companyCode = null)
        {
            try
            {
                var effectiveCompanyCode = ValidateKycCompanyCode(companyCode)
                                           ?? GetUserCompanyCode();

                var shareType = await _shareTypeService.GetShareTypeByCodeAsync(sharesCode, effectiveCompanyCode);
                return View(shareType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error loading details for share type {sharesCode}");
                TempData["ErrorMessage"] = $"Error loading share type details.";
                return RedirectToAction("Index");
            }
        }

        // ============================================================
        // GET: /ShareTypeMvc/Delete/{sharesCode}
        // ============================================================
        public async Task<IActionResult> Delete(string sharesCode, string companyCode = null)
        {
            try
            {
                var effectiveCompanyCode = ValidateKycCompanyCode(companyCode)
                                           ?? GetUserCompanyCode();

                var shareType = await _shareTypeService.GetShareTypeByCodeAsync(sharesCode, effectiveCompanyCode);
                return View(shareType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error loading delete confirmation for share type {sharesCode}");
                TempData["ErrorMessage"] = $"Error loading share type details.";
                return RedirectToAction("Index");
            }
        }

        // ============================================================
        // POST: /ShareTypeMvc/DeleteConfirmed
        // ============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(string sharesCode, string companyCode = null)
        {
            try
            {
                var effectiveCompanyCode = ValidateKycCompanyCode(companyCode)
                                           ?? GetUserCompanyCode();

                var shareType = await _shareTypeService.GetShareTypeByCodeAsync(sharesCode, effectiveCompanyCode);
                var shareTypeName = shareType.SharesType;

                await _shareTypeService.DeleteShareTypeAsync(sharesCode, effectiveCompanyCode);

                TempData["SuccessMessage"] = $"Share type '{shareTypeName}' deleted successfully!";
                return RedirectToAction("Index");
            }
            catch (ValidationException ex)
            {
                _logger.LogWarning(ex, $"Cannot delete share type {sharesCode}");
                TempData["ErrorMessage"] = ex.Message;
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting share type {sharesCode}");
                TempData["ErrorMessage"] = $"Error deleting share type: {ex.Message}";
                return RedirectToAction("Index");
            }
        }

        // ============================================================
        // GET: /ShareTypeMvc/GetShareTypeDetails
        // ============================================================
        [HttpGet]
        public async Task<IActionResult> GetShareTypeDetails(string sharesCode, string companyCode = null)
        {
            try
            {
                var effectiveCompanyCode = ValidateKycCompanyCode(companyCode)
                                           ?? GetUserCompanyCode();

                var shareType = await _shareTypeService.GetShareTypeByCodeAsync(sharesCode, effectiveCompanyCode);

                if (shareType == null)
                    return Json(new { success = false, message = "Share type not found" });

                return Json(new
                {
                    success = true,
                    data = new
                    {
                        shareType.SharesCode,
                        shareType.SharesType,
                        shareType.SharesAcc,
                        shareType.MinAmount,
                        MaxAmount = shareType.MaxAmount ?? 0,
                        LoanToShareRatio = shareType.LoanToShareRatio ?? 0,
                        shareType.IsMainShares,
                        shareType.Issharecapital,
                        shareType.Withdrawable,
                        shareType.UsedToOffset,
                        shareType.UsedToGuarantee,
                        shareType.CompanyCode
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching share type details");
                return Json(new { success = false, message = "Error retrieving data" });
            }
        }

        // ============================================================
        // GET: /ShareTypeMvc/GetAccountDetails
        // ============================================================
        [HttpGet]
        public async Task<IActionResult> GetAccountDetails(string accountCode, string companyCode = null)
        {
            try
            {
                var effectiveCompanyCode = ValidateKycCompanyCode(companyCode)
                                           ?? GetUserCompanyCode();

                var account = await _glAccountService.GetGlAccountByCodeAsync(accountCode, effectiveCompanyCode);

                if (account != null)
                {
                    return Json(new
                    {
                        success = true,
                        accountName = account.Glaccname,
                        accountCode = account.AccNo,
                    });
                }

                return Json(new { success = false, message = "Account not found" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting account details for code: {accountCode}");
                return Json(new { success = false, message = "Error retrieving account details" });
            }
        }

        // ============================================================
        // Fallback: session company code
        // ============================================================
        private string GetUserCompanyCode()
        {
            var companyCode = User.FindFirst("CompanyCode")?.Value;
            if (string.IsNullOrEmpty(companyCode))
            {
                companyCode = HttpContext.Session.GetString("CompanyCode");
            }

            if (string.IsNullOrEmpty(companyCode))
            {
                throw new Exception("Company code not found. Please log in again.");
            }

            return companyCode;
        }
    }
}