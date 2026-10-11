using AutoMapper.Execution;
using KYCSACCO.Data;
using KYCSACCO.Models;
using KYCSACCO.Models.ViewModels;
using KYCSACCO.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using KYCSACCO.Data;
using KYCSACCO.Models;
using KYCSACCO.Models.DTOs;
using KYCSACCO.Models.ViewModels;
using KYCSACCO.Services;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace KYCSACCO.Controllers
{
    public class AccountController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<AccountController> _logger;
        private readonly IUserService _userService;
        private readonly IOtpService _otpService;
        private readonly IUserSessionService _sessionService;

        public AccountController(ApplicationDbContext context, IUserService userService, ILogger<AccountController> logger, IOtpService otpService, IUserSessionService sessionService)
        {
            _context = context;
            _userService = userService;
            _logger = logger;
            _otpService = otpService;
            _sessionService = sessionService;
        }

        // GET: /Account/Login
        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            if (User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Index", "Home");
            }

            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        //// POST: /Account/Login
        //[HttpPost]
        //[ValidateAntiForgeryToken]
        //public async Task<IActionResult> Login(LoginVm model, string? returnUrl = null)
        //{
        //    if (!ModelState.IsValid)
        //    {
        //        return View(model);
        //    }

        //    try
        //    {
        //        if (model.MemberLogin == "false")
        //        {
        //            var hashedPassword = HashPassword(model.Password);

        //            // Find user by Username and password
        //            var user = await _context.UserAccounts1
        //                .FirstOrDefaultAsync(u => u.UserName == model.Username && u.Password == hashedPassword);

        //            if (user == null)
        //            {
        //                // Update failed attempts for the username
        //                var failedUser = await _context.UserAccounts1
        //                    .FirstOrDefaultAsync(u => u.UserName == model.Username);

        //                if (failedUser != null)
        //                {
        //                    failedUser.FailedAttempts = (failedUser.FailedAttempts ?? 0) + 1;

        //                    // Lock account after 5 failed attempts
        //                    if (failedUser.FailedAttempts >= 5)
        //                    {
        //                        failedUser.IsLocked = true;
        //                        _logger.LogWarning($"Account locked for username: {model.Username}");
        //                    }

        //                    await _context.SaveChangesAsync();
        //                }

        //                ModelState.AddModelError(string.Empty, "Invalid username or password.");
        //                return View(model);
        //            }

        //            // Check if account is locked
        //            if (user.IsLocked == true)
        //            {
        //                ModelState.AddModelError(string.Empty, "Account is locked. Please contact administrator.");
        //                return View(model);
        //            }

        //            // Check if account is active
        //            if (user.Status?.ToLower() != "active" && user.Userstatus?.ToLower() != "active")
        //            {
        //                ModelState.AddModelError(string.Empty, "Account is not active. Please contact administrator.");
        //                return View(model);
        //            }

        //            // check if account is approved
        //            if (user.ApprovalStatus?.ToLower() != "approved")
        //            {
        //                ModelState.AddModelError(string.Empty, "Account is not approved. Please contact administrator.");
        //                return View(model);
        //            }

        //            // Check if user has a company code
        //            if (string.IsNullOrEmpty(user.CompanyCode))
        //            {
        //                ModelState.AddModelError(string.Empty, "User account is not associated with any company. Please contact administrator.");
        //                return View(model);
        //            }

        //            // Get company name for the user's company code
        //            var company = await _context.Companies
        //                .FirstOrDefaultAsync(c => c.CompanyCode == user.CompanyCode);

        //            var companyName = company?.CompanyName ?? "Unknown Company";

        //            // Reset failed attempts on successful login
        //            user.FailedAttempts = 0;
        //            await _context.SaveChangesAsync();

        //            var claims = new List<Claim>
        //            {
        //                new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
        //                new Claim(ClaimTypes.Name, user.UserName),
        //                new Claim("FullName", user.UserName ?? string.Empty),
        //                new Claim("Email", user.Email ?? string.Empty),
        //                new Claim("UserId", user.UserId.ToString()),
        //                new Claim("CompanyCode", user.CompanyCode ?? "000"),
        //                new Claim("CompanyName", companyName),
        //                new Claim("UserLoginId", user.UserLoginId ?? string.Empty)
        //            };

        //            // Add UserGroup as a separate claim - THIS IS IMPORTANT
        //            if (!string.IsNullOrEmpty(user.UserGroup))
        //            {
        //                claims.Add(new Claim("UserGroup", user.UserGroup));
        //                claims.Add(new Claim(ClaimTypes.Role, user.UserGroup));
        //            }

        //            // Add additional user info claims
        //            if (!string.IsNullOrEmpty(user.Department))
        //            {
        //                claims.Add(new Claim("Department", user.Department));
        //            }

        //            if (!string.IsNullOrEmpty(user.MemberNo))
        //            {
        //                claims.Add(new Claim("MemberNo", user.MemberNo));
        //            }

        //            //CompanyCode claim
        //            if (!string.IsNullOrEmpty(user.CompanyCode))
        //            {
        //                claims.Add(new Claim("CompanyCode", user.CompanyCode));
        //            }

        //            // Branch code Claims
        //            if (!string.IsNullOrEmpty(user.Branchcode))
        //            {
        //                claims.Add(new Claim("BranchCode", user.Branchcode));
        //            }

        //            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

        //            var authProperties = new AuthenticationProperties
        //            {
        //                IsPersistent = model.RememberMe,
        //                ExpiresUtc = model.RememberMe ? DateTimeOffset.UtcNow.AddDays(30) : DateTimeOffset.UtcNow.AddHours(2),
        //                RedirectUri = returnUrl ?? "/MembersMVC/Index"
        //            };

        //            await HttpContext.SignInAsync(
        //                CookieAuthenticationDefaults.AuthenticationScheme,
        //                new ClaimsPrincipal(claimsIdentity),
        //                authProperties);

        //            // ============================================================
        //            // ✅ CREATE USER SESSION RECORD
        //            // ============================================================
        //            var userSession = await _sessionService.CreateSessionAsync(
        //                userId: user.UserId.ToString(),
        //                userName: user.UserName,
        //                companyCode: user.CompanyCode,
        //                companyName: companyName,
        //                userGroup: user.UserGroup ?? "Member"
        //            );

        //            // Store session ID in session for later use
        //            if (userSession != null)
        //            {
        //                HttpContext.Session.SetString("CurrentSessionId", userSession.SessionId.ToString());
        //                _logger.LogInformation($"User session created with ID: {userSession.SessionId}");
        //            }

        //            _logger.LogInformation($"User {user.UserName} (Company: {companyName} - {user.CompanyCode}) logged in successfully.");


        //            // comment this if you what otp for login
        //            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
        //            {
        //                return Redirect(returnUrl);
        //            }
        //            return RedirectToAction("Index", "Home");

        //            //// Redirect to OTP verification after successful login
        //            //return RedirectToAction("LoginOTPVerification", new { returnUrl = returnUrl });
        //        }
        //        else
        //        {
        //            // Try MemberNo first, then Idno. Select ONLY the columns we actually use.
        //            var user = await _context.Members
        //                .AsNoTracking()
        //                .Where(m => m.MemberNo == model.Username)
        //                .Select(m => new Member
        //                {
        //                    Id = m.Id,
        //                    MemberNo = m.MemberNo,
        //                    Surname = m.Surname,
        //                    OtherNames = m.OtherNames,
        //                    UserName = m.UserName,
        //                    Email = m.Email,
        //                    Employer = m.Employer,
        //                    CompanyCode = m.CompanyCode,
        //                    Pin = m.Pin,
        //                    Status = m.Status,
        //                    Idno = m.Idno
        //                })
        //                .FirstOrDefaultAsync();

        //            if (user == null)
        //            {
        //                user = await _context.Members
        //                    .AsNoTracking()
        //                    .Where(m => m.Idno == model.Username)
        //                    .Select(m => new Member
        //                    {
        //                        Id = m.Id,
        //                        MemberNo = m.MemberNo,
        //                        Surname = m.Surname,
        //                        OtherNames = m.OtherNames,
        //                        UserName = m.UserName,
        //                        Email = m.Email,
        //                        Employer = m.Employer,
        //                        CompanyCode = m.CompanyCode,
        //                        Pin = m.Pin,
        //                        Status = m.Status,
        //                        Idno = m.Idno
        //                    })
        //                    .FirstOrDefaultAsync();
        //            }

        //            if (user == null)
        //            {
        //                ModelState.AddModelError(string.Empty, "Invalid username.");
        //                return View(model);
        //            }

        //            if (!string.IsNullOrEmpty(user.Pin))
        //            {
        //                HttpContext.Session.SetString($"PinSetFor_{model.Username}", "false");
        //                return Redirect("/Account/VerifyMember?requestpin=true&user=" + EncryptionHelper.Encrypt(user.MemberNo));
        //            }
        //            else
        //            {
        //                return await SendCode(user);
        //            }
        //        }
        //        // Hash the password for comparison

        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "Error during login");
        //        ModelState.AddModelError(string.Empty, "An error occurred during login. Please try again.");
        //        return View(model);
        //    }
        //}


        // POST: /Account/Login
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginVm model, string? returnUrl = null)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            try
            {
                var hashedPassword = HashPassword(model.Password);

                // Find user by Username and password
                var user = await _context.UserAccounts1
                    .FirstOrDefaultAsync(u => u.UserName == model.Username && u.Password == hashedPassword);

                if (user == null)
                {
                    // Update failed attempts for the username
                    var failedUser = await _context.UserAccounts1
                        .FirstOrDefaultAsync(u => u.UserName == model.Username);

                    if (failedUser != null)
                    {
                        failedUser.FailedAttempts = (failedUser.FailedAttempts ?? 0) + 1;

                        // Lock account after 5 failed attempts
                        if (failedUser.FailedAttempts >= 5)
                        {
                            failedUser.IsLocked = true;
                            _logger.LogWarning($"Account locked for username: {model.Username}");
                        }

                        await _context.SaveChangesAsync();
                    }

                    ModelState.AddModelError(string.Empty, "Invalid username or password.");
                    return View(model);
                }

                // Check if account is locked
                if (user.IsLocked == true)
                {
                    ModelState.AddModelError(string.Empty, "Account is locked. Please contact administrator.");
                    return View(model);
                }

                // Check if account is active
                if (user.Status?.ToLower() != "active" && user.Userstatus?.ToLower() != "active")
                {
                    ModelState.AddModelError(string.Empty, "Account is not active. Please contact administrator.");
                    return View(model);
                }

                // Check if account is approved
                if (user.ApprovalStatus?.ToLower() != "approved")
                {
                    ModelState.AddModelError(string.Empty, "Account is not approved. Please contact administrator.");
                    return View(model);
                }

                // Check if user has a company code
                if (string.IsNullOrEmpty(user.CompanyCode))
                {
                    ModelState.AddModelError(string.Empty, "User account is not associated with any company. Please contact administrator.");
                    return View(model);
                }

                // Get company name for the user's company code
                var company = await _context.Companies
                    .FirstOrDefaultAsync(c => c.CompanyCode == user.CompanyCode);

                var companyName = company?.CompanyName ?? "Unknown Company";

                // Reset failed attempts on successful login
                user.FailedAttempts = 0;
                await _context.SaveChangesAsync();

                var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
            new Claim(ClaimTypes.Name, user.UserName),
            new Claim("FullName", user.UserName ?? string.Empty),
            new Claim("Email", user.Email ?? string.Empty),
            new Claim("UserId", user.UserId.ToString()),
            new Claim("CompanyCode", user.CompanyCode ?? "000"),
            new Claim("CompanyName", companyName),
            new Claim("UserLoginId", user.UserLoginId ?? string.Empty)
        };

                // Add UserGroup as a separate claim - THIS IS IMPORTANT
                if (!string.IsNullOrEmpty(user.UserGroup))
                {
                    claims.Add(new Claim("UserGroup", user.UserGroup));
                    claims.Add(new Claim(ClaimTypes.Role, user.UserGroup));
                }

                // Add additional user info claims
                if (!string.IsNullOrEmpty(user.Department))
                {
                    claims.Add(new Claim("Department", user.Department));
                }

                if (!string.IsNullOrEmpty(user.MemberNo))
                {
                    claims.Add(new Claim("MemberNo", user.MemberNo));
                }

                // CompanyCode claim
                if (!string.IsNullOrEmpty(user.CompanyCode))
                {
                    claims.Add(new Claim("CompanyCode", user.CompanyCode));
                }

                // Branch code claim
                if (!string.IsNullOrEmpty(user.Branchcode))
                {
                    claims.Add(new Claim("BranchCode", user.Branchcode));
                }

                var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

                var authProperties = new AuthenticationProperties
                {
                    IsPersistent = model.RememberMe,
                    ExpiresUtc = model.RememberMe ? DateTimeOffset.UtcNow.AddDays(30) : DateTimeOffset.UtcNow.AddHours(2),
                    RedirectUri = returnUrl ?? "/MembersMVC/Index"
                };

                await HttpContext.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    new ClaimsPrincipal(claimsIdentity),
                    authProperties);

                // ============================================================
                // ✅ CREATE USER SESSION RECORD
                // ============================================================
                var userSession = await _sessionService.CreateSessionAsync(
                    userId: user.UserId.ToString(),
                    userName: user.UserName,
                    companyCode: user.CompanyCode,
                    companyName: companyName,
                    userGroup: user.UserGroup ?? "Member"
                );

                // Store session ID in session for later use
                if (userSession != null)
                {
                    HttpContext.Session.SetString("CurrentSessionId", userSession.SessionId.ToString());
                    _logger.LogInformation($"User session created with ID: {userSession.SessionId}");
                }

                _logger.LogInformation($"User {user.UserName} (Company: {companyName} - {user.CompanyCode}) logged in successfully.");

                // Comment this if you want OTP for login
                if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                {
                    return Redirect(returnUrl);
                }
                return RedirectToAction("Index", "Home");

                //// Redirect to OTP verification after successful login
                //return RedirectToAction("LoginOTPVerification", new { returnUrl = returnUrl });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during login");
                ModelState.AddModelError(string.Empty, "An error occurred during login. Please try again.");
                return View(model);
            }
        }


        // GET: /Account/Signup
        [HttpGet]
        public async Task<IActionResult> Signup()
        {
            if (User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Index", "Blockchain");
            }

            await LoadCompanies();

            var model = new SignupVm
            {
                AvailableUserGroups = GetUserGroups()
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Signup(SignupVm model)
        {
            // Debug: Log what's being received
            _logger.LogInformation("=== SIGNUP ATTEMPT ===");
            _logger.LogInformation($"Username: {model.UserName}");
            _logger.LogInformation($"Email: {model.Email}");
            _logger.LogInformation($"CompanyCode: {model.CompanyCode}");
            _logger.LogInformation($"ModelState.IsValid: {ModelState.IsValid}");

            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Model validation failed!");
                foreach (var key in ModelState.Keys)
                {
                    var errors = ModelState[key].Errors;
                    if (errors.Any())
                    {
                        _logger.LogWarning($"  {key}: {string.Join(", ", errors.Select(e => e.ErrorMessage))}");
                    }
                }

                await LoadCompanies();
                return View(model);
            }

            // --- Reject attempts to assign Super Admin by anyone who isn't one ---
            var currentRole = User.FindFirstValue(ClaimTypes.Role);
            var currentGroup = User.FindFirstValue("UserGroup");

            bool isSuperAdmin =
                currentRole == "Super Admin" || currentGroup == "Super Admin" ||
                currentRole == "SuperAdmin" || currentGroup == "SuperAdmin";

            if (!isSuperAdmin &&
                (string.Equals(model.UserGroup, "Super Admin", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(model.UserGroup, "SuperAdmin", StringComparison.OrdinalIgnoreCase)))
            {
                TempData["ErrorMessage"] = "You are not allowed to assign the Super Admin role.";
                return RedirectToAction("Index");
            }

            try
            {
                // Validate company exists
                var company = await _context.Companies
                .FirstOrDefaultAsync(c => c.CompanyCode == model.CompanyCode && c.Project == true);

                if (company == null)
                {
                    ModelState.AddModelError("CompanyCode", "Selected company is not available.");
                    await LoadCompanies();
                    return View(model);
                }

                // Check for duplicate username
                var existingUser = await _context.UserAccounts1
                .FirstOrDefaultAsync(u => u.UserName == model.UserName);

                if (existingUser != null)
                {
                    ModelState.AddModelError("UserName", "Username already exists.");
                    await LoadCompanies();
                    return View(model);
                }

                // Check for duplicate email
                if (!string.IsNullOrEmpty(model.Email))
                {
                    var existingEmail = await _context.UserAccounts1
                    .FirstOrDefaultAsync(u => u.Email == model.Email);

                    if (existingEmail != null)
                    {
                        ModelState.AddModelError("Email", "Email already registered.");
                        await LoadCompanies();
                        return View(model);
                    }
                }


                // Create the new user
                var user = new UserAccounts1
                {
                    UserName = model.UserName.Trim(),
                    UserLoginId = GenerateUserLoginId(model.UserName),
                    Password = HashPassword(model.Password),
                    Email = model.Email?.Trim(),
                    Phone = model.Phone?.Trim(),
                    PhoneNo = model.Phone?.Trim(),
                    //MemberNo = model.MemberNo?.Trim(),
                    Department = model.Department?.Trim(),
                    SubCounty = model.SubCounty?.Trim(),
                    Ward = model.Ward?.Trim(),
                    DateCreated = DateTime.Now,
                    Status = "Pending",
                    Userstatus = "Pending",
                    ApprovalStatus = "Pending",
                    FailedAttempts = 0,
                    IsLocked = false,
                    PasswordStatus = "Active",
                    PassExpire = "No",
                    UserGroup = string.IsNullOrEmpty(model.UserGroup) ? "Member" : model.UserGroup,
                    Cigcode = company.Cigcode,
                    CompanyCode = model.CompanyCode,
                    Branchcode = company.Cigcode ?? model.CompanyCode,
                    Superuser = 0,
                    Authorize = false,
                    Count = 0
                };

                // Save to database
                _context.UserAccounts1.Add(user);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"SUCCESS: User '{user.UserName}' created with ID: {user.UserId}");

                TempData["SuccessMessage"] = $"Registration successful! Your account is pending approval for {company.CompanyName}. You will be notified once approved.";
                return RedirectToAction("Login");
            }
            catch (DbUpdateException dbEx)
            {
                _logger.LogError(dbEx, "Database error during signup");
                ModelState.AddModelError(string.Empty, "A database error occurred. Please try again.");

                if (dbEx.InnerException != null)
                {
                    _logger.LogError($"Inner exception: {dbEx.InnerException.Message}");
                }

                await LoadCompanies();
                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during signup");
                ModelState.AddModelError(string.Empty, "An unexpected error occurred. Please try again.");
                await LoadCompanies();
                return View(model);
            }
        }


        //  distinct roles list (no duplicates within this list)
        private List<string> GetUserGroups()
        {
            return new List<string>
            {
                "Member",
                "Teller",
                "Administrator",
                "System Admin",
                "Loan Officer",
                "Auditor",
                "Book Keeper",
                "Finance Officer",
                "Board Member",
                "Staff"
            };
        }

        // Helper method to check if a role is a "System User" (privileged)
        private bool IsSystemUserRole(string userGroup)
        {
            var systemRoles = new List<string>
            {
                "Administrator",
                "System Admin",
                "Super Admin"
            };
            return systemRoles.Contains(userGroup);
        }

        // Helper method to count system users in a company
        private async Task<int> CountSystemUsersByCompanyAsync(string companyCode)
        {
            var users = await _context.UserAccounts1
                .Where(u => u.CompanyCode == companyCode)
                .ToListAsync();

            return users.Count(u => IsSystemUserRole(u.UserGroup));
        }

        // Helper method to check if a role is already taken in a company
        private async Task<bool> IsRoleAlreadyAssignedInCompanyAsync(string companyCode, string userGroup)
        {
            return await _context.UserAccounts1
                .AnyAsync(u => u.CompanyCode == companyCode && u.UserGroup == userGroup);
        }

        private async Task LoadCompanies()
        {
            try
            {
                var companies = await _context.Companies
                .Where(c => c.Project == true)
                .OrderBy(c => c.CompanyName)
                .Select(c => new
                {
                    c.CompanyCode,
                    DisplayText = $"{c.CompanyCode} - {c.CompanyName}"
                })
                .ToListAsync();

                ViewBag.Companies = companies;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load companies");
                ViewBag.Companies = new List<dynamic>();
            }
        }

        [HttpGet]
        [Authorize]
        public async Task<IActionResult> Profile()
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
            var user = await _context.UserAccounts1
                .FirstOrDefaultAsync(u => u.UserId == userId);

            if (user == null)
            {
                return NotFound();
            }

            // Get company name
            var company = await _context.Companies
                .FirstOrDefaultAsync(c => c.CompanyCode == user.CompanyCode);

            var profile = new ProfileVm
            {
                UserId = user.UserId,
                UserName = user.UserName,
                UserLoginId = user.UserLoginId,
                Email = user.Email,
                Phone = user.Phone,
                MemberNo = user.MemberNo,
                Department = user.Department,
                SubCounty = user.SubCounty,
                Ward = user.Ward,
                UserGroup = user.UserGroup,
                Status = user.Status,
                DateCreated = user.DateCreated
            };

            ViewBag.CompanyName = company?.CompanyName ?? "Unknown Company";
            ViewBag.CompanyCode = user.CompanyCode;

            return View(profile);
        }

        // GET: /Account/Logout
        [HttpGet]
        public async Task<IActionResult> Logout()
        {
            try
            {
                var userName = User.Identity?.Name;
                var companyName = User.FindFirstValue("CompanyName");
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

                // ============================================================
                // ✅ UPDATE SESSION LOGOUT TIME
                // ============================================================
                var sessionIdStr = HttpContext.Session.GetString("CurrentSessionId");
                if (!string.IsNullOrEmpty(sessionIdStr) && long.TryParse(sessionIdStr, out long sessionId))
                {
                    await _sessionService.UpdateSessionLogoutAsync(sessionId, "Manual");
                    _logger.LogInformation($"Session {sessionId} logged out for user {userName}");
                }

                await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                HttpContext.Session.Clear();

                _logger.LogInformation($"User {userName} (Company: {companyName}) logged out.");
                return RedirectToAction("Login", "Account");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during logout");
                await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                HttpContext.Session.Clear();
                return RedirectToAction("Login", "Account");
            }
        }

        // POST: /Account/Logout - For form submissions
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> LogoutPost()
        {
            try
            {
                var userName = User.Identity?.Name;
                var companyName = User.FindFirstValue("CompanyName");
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

                var sessionIdStr = HttpContext.Session.GetString("CurrentSessionId");
                if (!string.IsNullOrEmpty(sessionIdStr) && long.TryParse(sessionIdStr, out long sessionId))
                {
                    await _sessionService.UpdateSessionLogoutAsync(sessionId, "Manual");
                }

                await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                HttpContext.Session.Clear();

                _logger.LogInformation($"User {userName} (Company: {companyName}) logged out.");
                return RedirectToAction("Login", "Account");
            }
            catch
            {
                await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                HttpContext.Session.Clear();
                return RedirectToAction("Login", "Account");
            }
        }


        [HttpGet]
        public IActionResult AccessDenied()
        {
            return View();
        }

        // GET: /Account/CompanySwitch
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> CompanySwitch(string searchTerm = null)
        {
            try
            {
                // ✅ Force refresh claims from database
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (!string.IsNullOrEmpty(userId))
                {
                    var user = await _context.UserAccounts1
                        .FirstOrDefaultAsync(u => u.UserId.ToString() == userId);

                    if (user != null)
                    {
                        // Update session with latest company data
                        var company = await _context.Companies
                            .FirstOrDefaultAsync(c => c.CompanyCode == user.CompanyCode);

                        if (company != null)
                        {
                            HttpContext.Session.SetString("CompanyCode", user.CompanyCode);
                            HttpContext.Session.SetString("CompanyName", company.CompanyName ?? "Unknown Company");
                        }
                    }
                }

                var currentUserRole = User.FindFirstValue(ClaimTypes.Role);
                var currentUserGroup = User.FindFirstValue("UserGroup");

                // Allow Super Admin, Admin, and System Administrator
                bool canSwitchCompanies = currentUserRole == "Super Admin" ||
                                          currentUserRole == "Admin" ||
                                          currentUserRole == "System Administrator" ||
                                          currentUserGroup == "Super Admin" ||
                                          currentUserGroup == "Admin" ||
                                          currentUserGroup == "System Administrator";

                if (!canSwitchCompanies)
                {
                    TempData["ErrorMessage"] = "You do not have permission to switch companies. Only Super Admin, Admin, and System Administrator can switch companies.";
                    return RedirectToAction("Index", "Home");
                }

                ViewBag.SearchTerm = searchTerm;

                var companiesQuery = _context.Companies.Where(c => c.Project == true);

                if (!string.IsNullOrEmpty(searchTerm))
                {
                    companiesQuery = companiesQuery.Where(c =>
                        c.CompanyCode.Contains(searchTerm) ||
                        c.CompanyName.Contains(searchTerm));
                }

                var companies = await companiesQuery
                    .OrderBy(c => c.CompanyName)
                    .Select(c => new
                    {
                        c.CompanyCode,
                        c.CompanyName,
                        DisplayText = $"{c.CompanyCode} - {c.CompanyName}"
                    })
                    .ToListAsync();

                ViewBag.Companies = companies;
                ViewBag.TotalCompanies = companies.Count;
                ViewBag.CurrentCompany = User.FindFirstValue("CompanyName") ?? "Unknown";

                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading company switch page");
                TempData["ErrorMessage"] = "Error loading companies list";
                return RedirectToAction("Index", "Home");
            }
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> SwitchCompany(string companyCode)
        {
            try
            {
                var currentUserRole = User.FindFirstValue(ClaimTypes.Role);
                var currentUserGroup = User.FindFirstValue("UserGroup");

                // Allow Super Admin, Admin, and System Administrator
                bool canSwitchCompanies = currentUserRole == "Super Admin" ||
                                          currentUserRole == "Admin" ||
                                          currentUserRole == "System Administrator" ||
                                          currentUserGroup == "Super Admin" ||
                                          currentUserGroup == "Admin" ||
                                          currentUserGroup == "System Administrator";

                if (!canSwitchCompanies)
                {
                    TempData["ErrorMessage"] = "You do not have permission to switch companies.";
                    return RedirectToAction("Index", "Home");
                }

                if (string.IsNullOrEmpty(companyCode))
                {
                    TempData["ErrorMessage"] = "Please select a company to switch to.";
                    return RedirectToAction("CompanySwitch");
                }

                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
                var user = await _context.UserAccounts1
                    .FirstOrDefaultAsync(u => u.UserId == userId);

                if (user == null)
                {
                    TempData["ErrorMessage"] = "User not found.";
                    return RedirectToAction("CompanySwitch");
                }

                // Validate new company
                var newCompany = await _context.Companies
                    .FirstOrDefaultAsync(c => c.CompanyCode == companyCode && c.Project == true);

                if (newCompany == null)
                {
                    TempData["ErrorMessage"] = $"Company with code {companyCode} not found or inactive.";
                    return RedirectToAction("CompanySwitch");
                }

                // Don't switch to the same company
                if (user.CompanyCode == companyCode)
                {
                    TempData["ErrorMessage"] = $"You are already in company {newCompany.CompanyName}.";
                    return RedirectToAction("CompanySwitch");
                }

                // Update user's company code
                user.CompanyCode = companyCode;
                await _context.SaveChangesAsync();

                // ============================================================
                // ✅ FIX: Rebuild claims with new company data
                // ============================================================

                // Sign out first to clear old claims
                await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

                // Create new claims with updated company data
                var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
            new Claim(ClaimTypes.Name, user.UserName),
            new Claim("FullName", user.UserName ?? string.Empty),
            new Claim("Email", user.Email ?? string.Empty),
            new Claim("UserId", user.UserId.ToString()),
            new Claim("CompanyCode", user.CompanyCode ?? "000"),
            new Claim("CompanyName", newCompany.CompanyName ?? "Unknown Company"),
            new Claim("UserLoginId", user.UserLoginId ?? string.Empty)
        };

                if (!string.IsNullOrEmpty(user.UserGroup))
                {
                    claims.Add(new Claim("UserGroup", user.UserGroup));
                    claims.Add(new Claim(ClaimTypes.Role, user.UserGroup));
                }

                if (!string.IsNullOrEmpty(user.Department))
                {
                    claims.Add(new Claim("Department", user.Department));
                }

                if (!string.IsNullOrEmpty(user.MemberNo))
                {
                    claims.Add(new Claim("MemberNo", user.MemberNo));
                }

                if (!string.IsNullOrEmpty(user.Branchcode))
                {
                    claims.Add(new Claim("BranchCode", user.Branchcode));
                }

                var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                var authProperties = new AuthenticationProperties
                {
                    IsPersistent = true,
                    ExpiresUtc = DateTimeOffset.UtcNow.AddDays(30)
                };

                // Sign in with new claims
                await HttpContext.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    new ClaimsPrincipal(claimsIdentity),
                    authProperties);

                // ============================================================
                // ✅ UPDATE SESSION WITH NEW COMPANY DATA
                // ============================================================

                // Update session variables with new company data
                HttpContext.Session.SetString("CompanyCode", companyCode);
                HttpContext.Session.SetString("CompanyName", newCompany.CompanyName ?? "Unknown Company");

                // Clear any cached data that might be company-specific
                HttpContext.Session.Remove("CurrentSessionId");
                HttpContext.Session.Remove("MemberNo");
                HttpContext.Session.Remove("MemberName");

                // Optional: Create a new session record for the new company
                var sessionIdStr = HttpContext.Session.GetString("CurrentSessionId");
                if (!string.IsNullOrEmpty(sessionIdStr) && long.TryParse(sessionIdStr, out long oldSessionId))
                {
                    // Close old session
                    await _sessionService.UpdateSessionLogoutAsync(oldSessionId, "CompanySwitch");
                }

                // Create new session for new company
                var userSession = await _sessionService.CreateSessionAsync(
                    userId: user.UserId.ToString(),
                    userName: user.UserName,
                    companyCode: companyCode,
                    companyName: newCompany.CompanyName ?? "Unknown Company",
                    userGroup: user.UserGroup ?? "Member"
                );

                if (userSession != null)
                {
                    HttpContext.Session.SetString("CurrentSessionId", userSession.SessionId.ToString());
                }

                _logger.LogInformation($"User {user.UserName} switched from old company to: {newCompany.CompanyName} ({companyCode})");

                // ✅ Return success with redirect to refresh the page
                TempData["SuccessMessage"] = $"Successfully switched to company: {newCompany.CompanyName}";

                // Redirect to refresh the page with new company data
                return RedirectToAction("Index", "Home");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error switching company to {companyCode}");
                TempData["ErrorMessage"] = $"Error switching company: {ex.Message}";
                return RedirectToAction("CompanySwitch");
            }
        }

        private string GenerateUserLoginId(string userName)
        {
            var prefix = userName.Length >= 3
            ? userName.Substring(0, 3).ToUpper()
            : userName.ToUpper();

            var timestamp = DateTime.Now.ToString("yyMMddHHmmss");
            return $"{prefix}{timestamp}";
        }
        private string HashPassword(string password)
        {
            using (var sha256 = SHA256.Create())
            {
                var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
                return Convert.ToBase64String(hashedBytes);
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetSubCountiesByCompany(string companyCode)
        {
            try
            {
                // Get company's CIG code or use company code to filter subcounties
                var company = await _context.Companies
                    .FirstOrDefaultAsync(c => c.CompanyCode == companyCode);

                if (company == null)
                {
                    return Json(new List<object>());
                }

                // Get subcounties associated with this company's region
                // You may need to adjust this based on your actual relationship
                var subCounties = await _context.SubCounties
                    .Where(s => s.Status == "Active")
                    .OrderBy(s => s.SubCountyName)
                    .Select(s => new { s.Id, s.SubCountyName, s.SubCountyCode })
                    .ToListAsync();

                return Json(subCounties);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading subcounties");
                return Json(new List<object>());
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetWardsBySubCounty(int subCountyId)
        {
            try
            {
                var wards = await _context.Wards
                    .Where(w => w.SubCountyId == subCountyId && w.Status == "Active")
                    .OrderBy(w => w.WardName)
                    .Select(w => new { w.Id, w.WardName, w.WardCode })
                    .ToListAsync();

                return Json(wards);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading wards");
                return Json(new List<object>());
            }
        }
        // GET: /Account/UserManagement
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> UserManagement(string searchTerm)
        {
            try
            {
                ViewBag.SearchTerm = searchTerm;

                var currentUserRole = User.FindFirstValue(ClaimTypes.Role);
                var currentUserCompanyCode = User.FindFirstValue("CompanyCode");

                List<KYCSACCO.Models.DTOs.UserListDTO> users;

                if (currentUserRole == "Super Admin")
                {
                    users = await _userService.GetAllUsersAsync(searchTerm);
                    ViewBag.Companies = await _userService.GetCompaniesForDropdownAsync();
                }
                else
                {
                    users = await _userService.GetUsersByCompanyAsync(currentUserCompanyCode, searchTerm);
                }

                // Convert DTO to ViewModel if needed, or use the same type
                var viewModel = new UserManagementViewModel
                {
                    Users = users.Select(u => new KYCSACCO.Models.ViewModels.UserListDTO
                    {
                        UserId = u.UserId,
                        UserName = u.UserName,
                        UserLoginId = u.UserLoginId,
                        Email = u.Email,
                        Phone = u.Phone,
                        Department = u.Department,
                        UserGroup = u.UserGroup,
                        CompanyCode = u.CompanyCode,
                        CompanyName = u.CompanyName,
                        Status = u.Status,
                        IsLocked = u.IsLocked
                    }).ToList(),
                    SearchTerm = searchTerm,
                    IsEditMode = false
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading user management page");
                TempData["ErrorMessage"] = "Error loading users list";
                return View(new UserManagementViewModel { Users = new List<KYCSACCO.Models.ViewModels.UserListDTO>() });
            }
        }

        public async Task<IActionResult> Index(string searchTerm)
        {
            try
            {
                ViewBag.SearchTerm = searchTerm;

                // Get current user's company code and role
                var currentUserCompanyCode = User.FindFirstValue("CompanyCode");
                var currentUserRole = User.FindFirstValue(ClaimTypes.Role);

                // Load users with company filtering
                List<KYCSACCO.Models.DTOs.UserListDTO> users;

                if (currentUserRole == "Super Admin")
                {
                    // Super Admin can see all users
                    users = await _userService.GetAllUsersAsync(searchTerm);
                }
                else
                {
                    // Regular users only see users from their company
                    users = await _userService.GetUsersByCompanyAsync(currentUserCompanyCode, searchTerm);
                }

                // Load dropdown data for the form (filtered by company for non-super admins)
                ViewBag.UserGroups = await GetVisibleUserGroupsAsync();

                if (currentUserRole == "Super Admin")
                {
                    ViewBag.Companies = await _userService.GetCompaniesForDropdownAsync();
                }
                else
                {
                    // Only show current user's company for non-super admins
                    var company = await _context.Companies
                        .Where(c => c.CompanyCode == currentUserCompanyCode && c.Project == true)
                        .Select(c => new { c.CompanyCode, DisplayText = $"{c.CompanyCode} - {c.CompanyName}" })
                        .FirstOrDefaultAsync();

                    ViewBag.Companies = company != null ? new List<dynamic> { company } : new List<dynamic>();
                }

                // Load subcounties and wards for dropdowns
                ViewBag.SubCounties = await _context.SubCounties
                    .Where(s => s.Status == "Active")
                    .OrderBy(s => s.SubCountyName)
                    .Select(s => new { s.Id, s.SubCountyName })
                    .ToListAsync();

                ViewBag.Wards = await _context.Wards
                    .Where(w => w.Status == "Active")
                    .OrderBy(w => w.WardName)
                    .Select(w => new { w.Id, w.WardName, w.SubCountyId })
                    .ToListAsync();

                // Create the view model with the users list - Convert DTO to ViewModel
                var viewModel = new UserManagementViewModel
                {
                    Users = users.Select(u => new KYCSACCO.Models.ViewModels.UserListDTO
                    {
                        UserId = u.UserId,
                        UserName = u.UserName,
                        UserLoginId = u.UserLoginId,
                        Email = u.Email,
                        Phone = u.Phone,
                        Department = u.Department,
                        UserGroup = u.UserGroup,
                        CompanyCode = u.CompanyCode,
                        CompanyName = u.CompanyName,
                        Status = u.Status,
                        IsLocked = u.IsLocked
                    }).ToList(),
                    SearchTerm = searchTerm,
                    IsEditMode = false
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading users list");
                TempData["ErrorMessage"] = "Error loading users list";

                var viewModel = new UserManagementViewModel
                {
                    Users = new List<KYCSACCO.Models.ViewModels.UserListDTO>(),
                    SearchTerm = searchTerm,
                    IsEditMode = false
                };

                return View(viewModel);
            }
        }

        // ============================================================
        // GET: /Account/CheckCompanyRoleAvailability
        // Returns which roles are already taken in the given company.
        // Used by the Create User modal to give live feedback BEFORE save.
        // ============================================================
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> CheckCompanyRoleAvailability(string companyCode)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(companyCode))
                {
                    return Json(new
                    {
                        success = false,
                        message = "Company code is required."
                    });
                }

                // Fetch all roles already assigned in this company
                var assignedRoles = await _context.UserAccounts1
                    .Where(u => u.CompanyCode == companyCode && u.UserGroup != null)
                    .Select(u => u.UserGroup)
                    .ToListAsync();

                // Get the full list of selectable roles for the current user
                var visibleRoles = await GetVisibleUserGroupsAsync();

                // Build a lookup: role name -> already taken?
                var takenLookup = assignedRoles
                    .GroupBy(r => r, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);

                // Count system users for the limit check
                var systemUsersCount = await CountSystemUsersByCompanyAsync(companyCode);

                var roles = visibleRoles.Select(r => new
                {
                    role = r,
                    isTaken = takenLookup.ContainsKey(r),
                    isSystemRole = IsSystemUserRole(r)
                }).ToList();

                return Json(new
                {
                    success = true,
                    companyCode = companyCode,
                    systemUsersCount = systemUsersCount,
                    systemUsersLimit = 5,
                    systemUsersFull = systemUsersCount >= 5,
                    roles = roles
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error checking role availability for company {companyCode}");
                return Json(new
                {
                    success = false,
                    message = "Error loading role availability."
                });
            }
        }

        // ============================================================
        // GET: /Account/CheckCompanyRoleAvailabilityForEdit
        // Returns which roles are taken in the given company, EXCLUDING
        // the user being edited (so their own role is not flagged as taken).
        // ============================================================
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> CheckCompanyRoleAvailabilityForEdit(string companyCode, int userId)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(companyCode))
                {
                    return Json(new { success = false, message = "Company code is required." });
                }

                if (userId <= 0)
                {
                    return Json(new { success = false, message = "UserId is required." });
                }

                // Fetch the user being edited (for permission check)
                var editingUser = await _context.UserAccounts1
                    .AsNoTracking()
                    .FirstOrDefaultAsync(u => u.UserId == userId);

                if (editingUser == null)
                {
                    return Json(new { success = false, message = "User not found." });
                }

                // Permission check
                var currentUserRole = User.FindFirstValue(ClaimTypes.Role);
                var currentUserCompanyCode = User.FindFirstValue("CompanyCode");

                if (currentUserRole != "Super Admin" && editingUser.CompanyCode != currentUserCompanyCode)
                {
                    return Json(new { success = false, message = "You don't have permission to check this user." });
                }

                // Fetch roles already assigned in this company, EXCLUDING the current user
                var assignedRoles = await _context.UserAccounts1
                    .Where(u => u.CompanyCode == companyCode
                             && u.UserId != userId
                             && u.UserGroup != null)
                    .Select(u => u.UserGroup)
                    .ToListAsync();

                var visibleRoles = await GetVisibleUserGroupsAsync();

                var takenLookup = assignedRoles
                    .GroupBy(r => r, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);

                // Count system users in this company, excluding the current user
                var allUsersInCompany = await _context.UserAccounts1
                    .Where(u => u.CompanyCode == companyCode && u.UserId != userId)
                    .ToListAsync();

                var systemUsersCount = allUsersInCompany.Count(u => IsSystemUserRole(u.UserGroup));

                // Also report the current user's own role, so we can mark it as "kept"
                var currentUserRoleValue = editingUser.UserGroup ?? string.Empty;

                var roles = visibleRoles.Select(r => new
                {
                    role = r,
                    isTaken = takenLookup.ContainsKey(r),
                    isSystemRole = IsSystemUserRole(r),
                    isCurrentUsersRole = string.Equals(r, currentUserRoleValue, StringComparison.OrdinalIgnoreCase)
                }).ToList();

                return Json(new
                {
                    success = true,
                    companyCode = companyCode,
                    systemUsersCount = systemUsersCount,
                    systemUsersLimit = 5,
                    systemUsersFull = systemUsersCount >= 5,
                    currentUserRole = currentUserRoleValue,
                    roles = roles
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error checking role availability for edit. Company={companyCode}, UserId={userId}");
                return Json(new { success = false, message = "Error loading role availability." });
            }
        }

        // Optional: Add a method to view company user statistics
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> CompanyUserStats(string companyCode)
        {
            if (string.IsNullOrEmpty(companyCode))
            {
                var currentUserCompanyCode = User.FindFirstValue("CompanyCode");
                companyCode = currentUserCompanyCode;
            }

            var allUsers = await _context.UserAccounts1
                .Where(u => u.CompanyCode == companyCode)
                .ToListAsync();

            var systemUsers = allUsers.Where(u => IsSystemUserRole(u.UserGroup)).ToList();
            var roleGroups = allUsers.GroupBy(u => u.UserGroup)
                .Select(g => new { Role = g.Key, Count = g.Count() })
                .ToList();

            var viewModel = new
            {
                CompanyCode = companyCode,
                TotalUsers = allUsers.Count,
                SystemUsersCount = systemUsers.Count,
                SystemUsersLimit = 5,
                SystemUsersRemaining = 5 - systemUsers.Count,
                RoleAssignments = roleGroups,
                HasDuplicateRoles = roleGroups.Any(r => r.Count > 1)
            };

            return Json(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(SignupVm model)
        {
            // Debug: Log what's being received
            _logger.LogInformation("=== CREATE USER ATTEMPT ===");
            _logger.LogInformation($"Username: {model.UserName}");
            _logger.LogInformation($"Email: {model.Email}");
            _logger.LogInformation($"CompanyCode: {model.CompanyCode}");
            _logger.LogInformation($"SubCountyId: {model.SubCountyId}");
            _logger.LogInformation($"WardId: {model.WardId}");
            _logger.LogInformation($"ModelState.IsValid: {ModelState.IsValid}");

            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Model validation failed!");
                foreach (var key in ModelState.Keys)
                {
                    var errors = ModelState[key].Errors;
                    if (errors.Any())
                    {
                        _logger.LogWarning($"  {key}: {string.Join(", ", errors.Select(e => e.ErrorMessage))}");
                    }
                }

                // Reload dropdown data
                await LoadCompanies();
                ViewBag.UserGroups = await GetVisibleUserGroupsAsync();

                TempData["ErrorMessage"] = "Please fix the validation errors.";
                return RedirectToAction("Index");
            }

            // --- Reject attempts to assign Super Admin by anyone who isn't one ---
            var currentRole = User.FindFirstValue(ClaimTypes.Role);
            var currentGroup = User.FindFirstValue("UserGroup");

            bool isSuperAdmin =
                currentRole == "Super Admin" || currentGroup == "Super Admin" ||
                currentRole == "SuperAdmin" || currentGroup == "SuperAdmin";

            if (!isSuperAdmin &&
                (string.Equals(model.UserGroup, "Super Admin", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(model.UserGroup, "SuperAdmin", StringComparison.OrdinalIgnoreCase)))
            {
                TempData["ErrorMessage"] = "You are not allowed to assign the Super Admin role.";
                return RedirectToAction("Index");
            }

            try
            {
                var company = await _context.Companies
                    .FirstOrDefaultAsync(c => c.CompanyCode == model.CompanyCode && c.Project == true);

                if (company == null)
                {
                    TempData["ErrorMessage"] = "Selected company is not available.";
                    return RedirectToAction("Index");
                }

                // NEW VALIDATION 1: Check if role already exists in this company
                if (await IsRoleAlreadyAssignedInCompanyAsync(model.CompanyCode, model.UserGroup))
                {
                    TempData["ErrorMessage"] = $"The role '{model.UserGroup}' is already assigned to another user in this company. Each role must be unique per company.";
                    return RedirectToAction("Index");
                }

                // NEW VALIDATION 2: Check system user limit (max 5)
                if (IsSystemUserRole(model.UserGroup))
                {
                    var systemUserCount = await CountSystemUsersByCompanyAsync(model.CompanyCode);
                    if (systemUserCount >= 5)
                    {
                        TempData["ErrorMessage"] = $"Cannot create more than 5 system users (Administrator/System Admin) in this company. Current count: {systemUserCount}/5";
                        return RedirectToAction("Index");
                    }
                }

                // Check for duplicate username
                var existingUser = await _context.UserAccounts1
                    .FirstOrDefaultAsync(u => u.UserName == model.UserName);

                if (existingUser != null)
                {
                    TempData["ErrorMessage"] = "Username already exists.";
                    return RedirectToAction("Index");
                }

                // Check for duplicate email
                if (!string.IsNullOrEmpty(model.Email))
                {
                    var existingEmail = await _context.UserAccounts1
                        .FirstOrDefaultAsync(u => u.Email == model.Email);

                    if (existingEmail != null)
                    {
                        TempData["ErrorMessage"] = "Email already registered.";
                        return RedirectToAction("Index");
                    }
                }

                // Get SubCounty and Ward names from IDs if selected
                string subCountyName = model.SubCounty;
                string wardName = model.Ward;

                if (model.SubCountyId.HasValue && model.SubCountyId.Value > 0)
                {
                    var subCounty = await _context.SubCounties
                        .FirstOrDefaultAsync(s => s.Id == model.SubCountyId.Value);
                    if (subCounty != null)
                    {
                        subCountyName = subCounty.SubCountyName;
                    }
                }

                if (model.WardId.HasValue && model.WardId.Value > 0)
                {
                    var ward = await _context.Wards
                        .FirstOrDefaultAsync(w => w.Id == model.WardId.Value);
                    if (ward != null)
                    {
                        wardName = ward.WardName;
                    }
                }

                // Create the new user
                var user = new UserAccounts1
                {
                    UserName = model.UserName.Trim(),
                    UserLoginId = GenerateUserLoginId(model.UserName),
                    Password = HashPassword(model.Password),
                    Email = model.Email?.Trim(),
                    Phone = model.Phone?.Trim(),
                    PhoneNo = model.Phone?.Trim(),
                    Department = model.Department?.Trim(),
                    SubCounty = subCountyName,
                    Ward = wardName,
                    DateCreated = DateTime.Now,
                    Status = "Pending", // Set to Active since admin creates
                    Userstatus = "Pending",
                    ApprovalStatus = "Pending",
                    FailedAttempts = 0,
                    IsLocked = false,
                    PasswordStatus = "Active",
                    PassExpire = "No",
                    UserGroup = string.IsNullOrEmpty(model.UserGroup) ? "Member" : model.UserGroup,
                    Cigcode = company.Cigcode,
                    CompanyCode = model.CompanyCode,
                    Branchcode = company.Cigcode ?? model.CompanyCode,
                    Superuser = 0,
                    Authorize = false,
                    Count = 0
                };

                // Save to database
                _context.UserAccounts1.Add(user);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"SUCCESS: User '{user.UserName}' created with ID: {user.UserId}");
                TempData["SuccessMessage"] = $"User '{user.UserName}' created successfully!";

                return RedirectToAction("Index");
            }
            catch (DbUpdateException dbEx)
            {
                _logger.LogError(dbEx, "Database error during user creation");
                TempData["ErrorMessage"] = "A database error occurred. Please try again.";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during user creation");
                TempData["ErrorMessage"] = "An unexpected error occurred. Please try again.";
                return RedirectToAction("Index");
            }
        }

        // GET: /Account/Edit/5
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> Edit(int id)
        {
            try
            {
                var user = await _context.UserAccounts1
                    .FirstOrDefaultAsync(u => u.UserId == id);

                if (user == null)
                {
                    TempData["ErrorMessage"] = "User not found.";
                    return RedirectToAction("Index");
                }

                // Check permissions - only Super Admin or users from same company can edit
                var currentUserRole = User.FindFirstValue(ClaimTypes.Role);
                var currentUserCompanyCode = User.FindFirstValue("CompanyCode");

                if (currentUserRole != "Super Admin" && user.CompanyCode != currentUserCompanyCode)
                {
                    TempData["ErrorMessage"] = "You don't have permission to edit this user.";
                    return RedirectToAction("Index");
                }

                // Load dropdown data
                await LoadCompanies();
                ViewBag.UserGroups = await GetVisibleUserGroupsAsync();

                // ===== FIX: Load SubCounties for dropdown =====
                ViewBag.SubCounties = await _context.SubCounties
                    .Where(s => s.Status == "Active")
                    .OrderBy(s => s.SubCountyName)
                    .Select(s => new { s.Id, s.SubCountyName })
                    .ToListAsync();

                // Get SubCounty and Ward IDs for dropdown selection
                int? subCountyId = null;
                int? wardId = null;

                if (!string.IsNullOrEmpty(user.SubCounty))
                {
                    var subCounty = await _context.SubCounties
                        .FirstOrDefaultAsync(s => s.SubCountyName == user.SubCounty);
                    if (subCounty != null)
                    {
                        subCountyId = subCounty.Id;
                    }
                }

                if (!string.IsNullOrEmpty(user.Ward))
                {
                    var ward = await _context.Wards
                        .FirstOrDefaultAsync(w => w.WardName == user.Ward);
                    if (ward != null)
                    {
                        wardId = ward.Id;
                    }
                }

                // Create the EditUserVm
                var model = new EditUserVm
                {
                    UserId = user.UserId,
                    UserName = user.UserName,
                    UserLoginId = user.UserLoginId,
                    Email = user.Email,
                    Phone = user.Phone,
                    Department = user.Department,
                    SubCounty = user.SubCounty,
                    Ward = user.Ward,
                    SubCountyId = subCountyId,
                    WardId = wardId,
                    UserGroup = user.UserGroup,
                    CompanyCode = user.CompanyCode,
                    Status = user.Status,
                    IsLocked = user.IsLocked ?? false,
                    DateCreated = user.DateCreated,
                    AvailableStatuses = new List<string> { "Active", "Inactive", "Pending", "Locked" }
                };

                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error in Edit GET for userId {id}");
                TempData["ErrorMessage"] = "An error occurred while loading the user data.";
                return RedirectToAction("Index");
            }
        }

        // POST: /Account/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> Edit(int id, EditUserVm model)
        {
            if (id != model.UserId)
            {
                TempData["ErrorMessage"] = "User ID mismatch.";
                return RedirectToAction("Index");
            }

            if (!ModelState.IsValid)
            {
                // Reload dropdown data
                await LoadCompanies();
                ViewBag.UserGroups = await GetVisibleUserGroupsAsync();

                // ===== FIX: Reload SubCounties when validation fails =====
                ViewBag.SubCounties = await _context.SubCounties
                    .Where(s => s.Status == "Active")
                    .OrderBy(s => s.SubCountyName)
                    .Select(s => new { s.Id, s.SubCountyName })
                    .ToListAsync();

                return View(model);
            }

            // --- Reject attempts to assign Super Admin by anyone who isn't one ---
            var currentRole = User.FindFirstValue(ClaimTypes.Role);
            var currentGroup = User.FindFirstValue("UserGroup");

            bool isSuperAdmin =
                currentRole == "Super Admin" || currentGroup == "Super Admin" ||
                currentRole == "SuperAdmin" || currentGroup == "SuperAdmin";

            if (!isSuperAdmin &&
                (string.Equals(model.UserGroup, "Super Admin", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(model.UserGroup, "SuperAdmin", StringComparison.OrdinalIgnoreCase)))
            {
                TempData["ErrorMessage"] = "You are not allowed to assign the Super Admin role.";
                return RedirectToAction("Index");
            }

            try
            {
                var user = await _context.UserAccounts1
                    .FirstOrDefaultAsync(u => u.UserId == id);

                if (user == null)
                {
                    TempData["ErrorMessage"] = "User not found.";
                    return RedirectToAction("Index");
                }

                // Check if role is being changed
                bool isRoleChanging = user.UserGroup != model.UserGroup;

                if (isRoleChanging)
                {
                    // VALIDATION 1: New role must not already exist in same company (excluding current user)
                    var roleExists = await _context.UserAccounts1
                        .AnyAsync(u => u.CompanyCode == user.CompanyCode &&
                                      u.UserGroup == model.UserGroup &&
                                      u.UserId != id);

                    if (roleExists)
                    {
                        TempData["ErrorMessage"] = $"The role '{model.UserGroup}' is already assigned to another user in this company. Each role must be unique per company.";
                        return RedirectToAction("Index");
                    }

                    // VALIDATION 2: Check system user limit if new role is system role
                    if (IsSystemUserRole(model.UserGroup) && !IsSystemUserRole(user.UserGroup))
                    {
                        var systemUserCount = await CountSystemUsersByCompanyAsync(user.CompanyCode);
                        if (systemUserCount >= 5)
                        {
                            TempData["ErrorMessage"] = $"Cannot add more system users. This company already has {systemUserCount}/5 system users.";
                            return RedirectToAction("Index");
                        }
                    }
                }

                // Check permissions
                var currentUserRole = User.FindFirstValue(ClaimTypes.Role);
                var currentUserCompanyCode = User.FindFirstValue("CompanyCode");

                if (currentUserRole != "Super Admin" && user.CompanyCode != currentUserCompanyCode)
                {
                    TempData["ErrorMessage"] = "You don't have permission to edit this user.";
                    return RedirectToAction("Index");
                }

                // Get SubCounty and Ward names from IDs if selected
                string subCountyName = model.SubCounty;
                string wardName = model.Ward;

                if (model.SubCountyId.HasValue && model.SubCountyId.Value > 0)
                {
                    var subCounty = await _context.SubCounties
                        .FirstOrDefaultAsync(s => s.Id == model.SubCountyId.Value);
                    if (subCounty != null)
                    {
                        subCountyName = subCounty.SubCountyName;
                    }
                }

                if (model.WardId.HasValue && model.WardId.Value > 0)
                {
                    var ward = await _context.Wards
                        .FirstOrDefaultAsync(w => w.Id == model.WardId.Value);
                    if (ward != null)
                    {
                        wardName = ward.WardName;
                    }
                }

                // Update user properties
                user.UserName = model.UserName?.Trim();
                user.Email = model.Email?.Trim();
                user.Phone = model.Phone?.Trim();
                user.PhoneNo = model.Phone?.Trim();
                user.Department = model.Department?.Trim();
                user.SubCounty = subCountyName;
                user.Ward = wardName;
                user.UserGroup = model.UserGroup;
                user.CompanyCode = model.CompanyCode;
                user.Status = model.Status;

                // If status is changed to Active, also update Userstatus
                if (model.Status == "Active")
                {
                    user.Userstatus = "Active";
                    if (model.Status == "Active" && user.IsLocked == true)
                    {
                        user.IsLocked = false;
                        user.FailedAttempts = 0;
                    }
                }
                else if (model.Status == "Locked")
                {
                    user.IsLocked = true;
                    user.Userstatus = "Locked";
                }
                else if (model.Status == "Pending")
                {
                    user.Userstatus = "Pending";
                    user.ApprovalStatus = "Pending";
                }

                await _context.SaveChangesAsync();

                _logger.LogInformation($"User {user.UserName} (ID: {user.UserId}) updated successfully by {User.Identity?.Name}");
                TempData["SuccessMessage"] = $"User '{user.UserName}' updated successfully.";

                return RedirectToAction("Index");
            }
            catch (DbUpdateException dbEx)
            {
                _logger.LogError(dbEx, $"Database error in Edit POST for userId {id}");
                ModelState.AddModelError(string.Empty, "A database error occurred while updating the user.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error in Edit POST for userId {id}");
                ModelState.AddModelError(string.Empty, "An unexpected error occurred while updating the user.");
            }

            // Reload dropdown data if there was an error
            await LoadCompanies();
            ViewBag.UserGroups = await GetVisibleUserGroupsAsync();

            // ===== FIX: Reload SubCounties when there's an error =====
            ViewBag.SubCounties = await _context.SubCounties
                .Where(s => s.Status == "Active")
                .OrderBy(s => s.SubCountyName)
                .Select(s => new { s.Id, s.SubCountyName })
                .ToListAsync();

            return View(model);
        }

        // GET: /Account/Details/5
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> Details(int id)
        {
            try
            {
                var user = await _context.UserAccounts1
                    .FirstOrDefaultAsync(u => u.UserId == id);

                if (user == null)
                {
                    TempData["ErrorMessage"] = "User not found.";
                    return RedirectToAction("Index");
                }

                // Check permissions
                var currentUserRole = User.FindFirstValue(ClaimTypes.Role);
                var currentUserCompanyCode = User.FindFirstValue("CompanyCode");

                if (currentUserRole != "Super Admin" && user.CompanyCode != currentUserCompanyCode)
                {
                    TempData["ErrorMessage"] = "You don't have permission to view this user.";
                    return RedirectToAction("Index");
                }

                // Get company name
                var company = await _context.Companies
                    .FirstOrDefaultAsync(c => c.CompanyCode == user.CompanyCode);

                var model = new UserDetailsVm
                {
                    UserId = user.UserId,
                    UserName = user.UserName,
                    UserLoginId = user.UserLoginId,
                    Email = user.Email,
                    Phone = user.Phone,
                    Department = user.Department,
                    SubCounty = user.SubCounty,
                    Ward = user.Ward,
                    UserGroup = user.UserGroup,
                    CompanyCode = user.CompanyCode,
                    CompanyName = company?.CompanyName ?? "Unknown",
                    Status = user.Status,
                    Userstatus = user.Userstatus,
                    ApprovalStatus = user.ApprovalStatus,
                    IsLocked = user.IsLocked ?? false,
                    FailedAttempts = user.FailedAttempts ?? 0,
                    DateCreated = user.DateCreated,
                    //DateModified = user.ModifiedAt,
                    //ModifiedBy = user.ModifiedBy,
                    PasswordStatus = user.PasswordStatus,
                    PassExpire = user.PassExpire
                };

                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error in Details for userId {id}");
                TempData["ErrorMessage"] = "An error occurred while loading user details.";
                return RedirectToAction("Index");
            }
        }

        // ============================================================
        // Role catalog, filtered by who the current user is:
        //   - Super Admin sees every role including "Super Admin"
        //   - Everyone else sees every role EXCEPT "Super Admin"
        // ============================================================
        private async Task<List<string>> GetVisibleUserGroupsAsync()
        {
            var currentRole = User.FindFirstValue(ClaimTypes.Role);
            var currentGroup = User.FindFirstValue("UserGroup");

            bool isSuperAdmin =
                currentRole == "Super Admin" || currentGroup == "Super Admin" ||
                currentRole == "SuperAdmin" || currentGroup == "SuperAdmin";

            var all = await _userService.GetUserGroupsAsync();

            if (isSuperAdmin)
                return all;

            // Remove protected roles from the list for non-Super-Admins
            var blocked = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "Super Admin",
                "SuperAdmin"
            };

            return all.Where(g => !blocked.Contains(g)).ToList();
        }

        // POST: /Account/LockUser
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> LockUser(int userId)
        {
            try
            {
                var user = await _context.UserAccounts1
                    .FirstOrDefaultAsync(u => u.UserId == userId);

                if (user == null)
                {
                    return Json(new { success = false, message = "User not found" });
                }

                // Check permissions
                var currentUserRole = User.FindFirstValue(ClaimTypes.Role);
                var currentUserCompanyCode = User.FindFirstValue("CompanyCode");

                if (currentUserRole != "Super Admin" && user.CompanyCode != currentUserCompanyCode)
                {
                    return Json(new { success = false, message = "You don't have permission to lock this user" });
                }

                user.IsLocked = true;
                user.Status = "Locked";
                user.Userstatus = "Locked";
                await _context.SaveChangesAsync();

                _logger.LogInformation($"User {user.UserName} locked by {User.Identity?.Name}");
                return Json(new { success = true, message = "User locked successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error locking user {userId}");
                return Json(new { success = false, message = "An error occurred" });
            }
        }


        [HttpGet]
        public IActionResult ForgotPassword()
        {
            if (User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Index", "Home");
            }
            return View();
        }

        // POST: /Account/ForgotPassword
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordVm model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            try
            {
                // Find user by username
                var user = await _context.UserAccounts1
                    .FirstOrDefaultAsync(u => u.UserName == model.Username);

                if (user == null)
                {
                    // Don't reveal that user doesn't exist for security
                    TempData["InfoMessage"] = "If the username exists, a verification code will be sent to your registered email.";
                    return RedirectToAction("ForgotPassword");
                }

                // Check if user has an email
                if (string.IsNullOrEmpty(user.Email))
                {
                    TempData["ErrorMessage"] = "No email address associated with this account. Please contact administrator.";
                    return RedirectToAction("ForgotPassword");
                }

                // Check if account is active
                if (user.Status?.ToLower() != "active" && user.Userstatus?.ToLower() != "active")
                {
                    TempData["ErrorMessage"] = "Your account is not active. Please contact administrator.";
                    return RedirectToAction("ForgotPassword");
                }

                // Generate 6-digit verification code
                var verificationCode = GenerateVerificationCode();

                // Store code with expiration (10 minutes from now)
                var codeExpiry = DateTime.Now.AddMinutes(10);

                // Store in TempData or Session (TempData is short-lived, use Session for better persistence)
                HttpContext.Session.SetString($"ResetCode_{model.Username}", verificationCode);
                HttpContext.Session.SetString($"ResetCodeExpiry_{model.Username}", codeExpiry.ToString("O"));

                // Store username for next steps
                TempData["ResetUsername"] = model.Username;

                // Send email with code
                var emailService = HttpContext.RequestServices.GetService<IEmailService>();
                if (emailService != null)
                {
                    var emailSent = await emailService.SendVerificationCodeAsync(user.Email, user.UserName, verificationCode);

                    if (emailSent)
                    {
                        TempData["SuccessMessage"] = $"Verification code sent to {MaskEmail(user.Email)}. Please check your email.";
                        return RedirectToAction("VerifyCode", new { username = model.Username });
                    }
                    else
                    {
                        TempData["ErrorMessage"] = "Failed to send verification email. Please try again later.";
                        return RedirectToAction("ForgotPassword");
                    }
                }
                else
                {
                    // For development/testing - show code on screen
                    _logger.LogWarning($"Email service not configured. Verification code for {model.Username}: {verificationCode}");
                    TempData["SuccessMessage"] = $"[DEV MODE] Verification code: {verificationCode}";
                    return RedirectToAction("VerifyCode", new { username = model.Username });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in ForgotPassword");
                TempData["ErrorMessage"] = "An error occurred. Please try again.";
                return View(model);
            }
        }

        // GET: /Account/VerifyCode
        [HttpGet]
        public IActionResult VerifyCode(string username)
        {
            if (User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Index", "Home");
            }

            if (string.IsNullOrEmpty(username))
            {
                return RedirectToAction("ForgotPassword");
            }

            var model = new VerifyCodeVm
            {
                Username = username
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VerifyCode(VerifyCodeVm model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            try
            {
                // Retrieve stored code and expiry
                var storedCode = HttpContext.Session.GetString($"ResetCode_{model.Username}");
                var expiryStr = HttpContext.Session.GetString($"ResetCodeExpiry_{model.Username}");

                if (string.IsNullOrEmpty(storedCode) || string.IsNullOrEmpty(expiryStr))
                {
                    TempData["ErrorMessage"] = "No verification code found. Please request a new code.";
                    return RedirectToAction("ForgotPassword");
                }

                // Check if code has expired
                if (DateTime.TryParse(expiryStr, out var expiry) && expiry < DateTime.Now)
                {
                    // Clear expired code
                    HttpContext.Session.Remove($"ResetCode_{model.Username}");
                    HttpContext.Session.Remove($"ResetCodeExpiry_{model.Username}");
                    TempData["ErrorMessage"] = "Verification code has expired. Please request a new code.";
                    return RedirectToAction("ForgotPassword");
                }

                // Verify code
                if (storedCode != model.Code)
                {
                    ModelState.AddModelError("Code", "Invalid verification code. Please try again.");
                    return View(model);
                }

                // Code is valid - clear it from session
                HttpContext.Session.Remove($"ResetCode_{model.Username}");
                HttpContext.Session.Remove($"ResetCodeExpiry_{model.Username}");

                // Store that user is verified for password reset
                HttpContext.Session.SetString($"VerifiedForReset_{model.Username}", "true");

                TempData["SuccessMessage"] = "Code verified successfully. Please enter your new password.";
                return RedirectToAction("ResetPassword", new { username = model.Username });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in VerifyCode");
                TempData["ErrorMessage"] = "An error occurred. Please try again.";
                return View(model);
            }
        }

        // GET: /Account/ResetPassword
        [HttpGet]
        public IActionResult ResetPassword(string username)
        {
            if (User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Index", "Home");
            }

            if (string.IsNullOrEmpty(username))
            {
                return RedirectToAction("ForgotPassword");
            }

            // Check if user is verified
            var isVerified = HttpContext.Session.GetString($"VerifiedForReset_{username}");
            if (string.IsNullOrEmpty(isVerified) || isVerified != "true")
            {
                TempData["ErrorMessage"] = "Please verify your code first.";
                return RedirectToAction("ForgotPassword");
            }

            var model = new ResetPasswordVm
            {
                Username = username
            };

            return View(model);
        }

        // POST: /Account/ResetPassword
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(ResetPasswordVm model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            try
            {
                // Check if user is verified
                var isVerified = HttpContext.Session.GetString($"VerifiedForReset_{model.Username}");
                if (string.IsNullOrEmpty(isVerified) || isVerified != "true")
                {
                    TempData["ErrorMessage"] = "Please verify your code first.";
                    return RedirectToAction("ForgotPassword");
                }

                // Find user
                var user = await _context.UserAccounts1
                    .FirstOrDefaultAsync(u => u.UserName == model.Username);

                if (user == null)
                {
                    TempData["ErrorMessage"] = "User not found.";
                    return RedirectToAction("ForgotPassword");
                }

                // Update only the password column
                user.Password = HashPassword(model.NewPassword);

                // Optional: Reset failed attempts and unlock if locked
                if (user.IsLocked == true)
                {
                    user.IsLocked = false;
                }
                user.FailedAttempts = 0;

                // Update password status
                user.PasswordStatus = "Active";
                user.PassExpire = "No";

                await _context.SaveChangesAsync();

                _logger.LogInformation($"Password reset for user {user.UserName} (ID: {user.UserId})");

                // Clear verification session
                HttpContext.Session.Remove($"VerifiedForReset_{model.Username}");

                TempData["SuccessMessage"] = "Password reset successfully! Please login with your new password.";
                return RedirectToAction("Login");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error resetting password for {model.Username}");
                TempData["ErrorMessage"] = "An error occurred while resetting password. Please try again.";
                return View(model);
            }
        }

        // Helper method to generate 6-digit code
        private string GenerateVerificationCode()
        {
            var random = new Random();
            return random.Next(100000, 999999).ToString();
        }

        // Helper method to mask email for display
        private string MaskEmail(string email)
        {
            if (string.IsNullOrEmpty(email) || !email.Contains("@"))
                return email;

            var parts = email.Split('@');
            var username = parts[0];
            var domain = parts[1];

            if (username.Length <= 3)
                return $"{username[0]}***@{domain}";

            var maskedUsername = username.Substring(0, 3) + new string('*', username.Length - 3);
            return $"{maskedUsername}@{domain}";
        }


        // ============= SUPER ADMIN APPROVAL METHODS =============
        // ============================================================
        // POST: /Account/ApproveUser - FIXED (Without Blockchain)
        // ============================================================
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveUser(int userId)
        {
            try
            {
                var currentUserRole = User.FindFirstValue(ClaimTypes.Role);
                var currentUserGroup = User.FindFirstValue("UserGroup");

                // Only Super Admin can approve
                bool isSuperAdmin = currentUserRole == "Super Admin" ||
                                    currentUserGroup == "Super Admin" ||
                                    currentUserRole == "SuperAdmin";

                if (!isSuperAdmin)
                {
                    return Json(new
                    {
                        success = false,
                        message = "Access denied. Only Super Administrators can approve users."
                    });
                }

                var user = await _context.UserAccounts1.FindAsync(userId);
                if (user == null)
                {
                    return Json(new { success = false, message = "User not found" });
                }

                // Check if user is already approved
                if (user.Status == "Active" && user.ApprovalStatus == "Approved")
                {
                    return Json(new
                    {
                        success = false,
                        message = $"User '{user.UserName}' is already approved."
                    });
                }

                // Update status to Active
                user.Status = "Active";
                user.Userstatus = "Active";
                user.ApprovalStatus = "Approved";
                user.IsLocked = false;
                user.FailedAttempts = 0;

                // ✅ Save changes WITHOUT blockchain transaction
                await _context.SaveChangesAsync();

                _logger.LogInformation($"User {user.UserName} (ID: {user.UserId}) approved by {User.Identity?.Name}");

                // Return success with user details
                return Json(new
                {
                    success = true,
                    message = $"✅ User '{user.UserName}' has been approved successfully! They can now log in.",
                    userName = user.UserName,
                    userId = user.UserId
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error approving user {userId}");
                return Json(new
                {
                    success = false,
                    message = $"Error approving user: {ex.Message}"
                });
            }
        }

        // ============================================================
        // POST: /Account/RejectUser - FIXED (Without Blockchain)
        // ============================================================
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectUser(int userId, string reason)
        {
            try
            {
                var currentUserRole = User.FindFirstValue(ClaimTypes.Role);
                var currentUserGroup = User.FindFirstValue("UserGroup");

                bool isSuperAdmin = currentUserRole == "Super Admin" ||
                                    currentUserGroup == "Super Admin" ||
                                    currentUserRole == "SuperAdmin";

                if (!isSuperAdmin)
                {
                    return Json(new
                    {
                        success = false,
                        message = "Access denied. Only Super Administrators can reject users."
                    });
                }

                var user = await _context.UserAccounts1.FindAsync(userId);
                if (user == null)
                {
                    return Json(new { success = false, message = "User not found" });
                }

                // Check if user is already rejected
                if (user.Status == "Rejected")
                {
                    return Json(new
                    {
                        success = false,
                        message = $"User '{user.UserName}' is already rejected."
                    });
                }

                // Update status to Rejected
                user.Status = "Rejected";
                user.Userstatus = "Rejected";
                user.ApprovalStatus = "Rejected";

                await _context.SaveChangesAsync();

                _logger.LogInformation($"User {user.UserName} (ID: {user.UserId}) rejected by {User.Identity?.Name}. Reason: {reason}");

                return Json(new
                {
                    success = true,
                    message = $"❌ User '{user.UserName}' has been rejected.",
                    userName = user.UserName,
                    userId = user.UserId
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error rejecting user {userId}");
                return Json(new
                {
                    success = false,
                    message = $"Error rejecting user: {ex.Message}"
                });
            }
        }

        // ============================================================
        // POST: /Account/BulkApproveUsers - FIXED (Without Blockchain)
        // ============================================================
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BulkApproveUsers([FromBody] List<int> userIds)
        {
            try
            {
                var currentUserRole = User.FindFirstValue(ClaimTypes.Role);
                var currentUserGroup = User.FindFirstValue("UserGroup");

                bool isSuperAdmin = currentUserRole == "Super Admin" ||
                                    currentUserGroup == "Super Admin" ||
                                    currentUserRole == "SuperAdmin";

                if (!isSuperAdmin)
                {
                    return Json(new
                    {
                        success = false,
                        message = "Access denied. Only Super Administrators can approve users."
                    });
                }

                if (userIds == null || !userIds.Any())
                {
                    return Json(new
                    {
                        success = false,
                        message = "No users selected for approval."
                    });
                }

                var users = await _context.UserAccounts1
                    .Where(u => userIds.Contains(u.UserId))
                    .ToListAsync();

                if (!users.Any())
                {
                    return Json(new
                    {
                        success = false,
                        message = "No valid users found."
                    });
                }

                int approvedCount = 0;
                var approvedUsers = new List<string>();

                foreach (var user in users)
                {
                    // Skip if already approved
                    if (user.Status == "Active" && user.ApprovalStatus == "Approved")
                        continue;

                    user.Status = "Active";
                    user.Userstatus = "Active";
                    user.ApprovalStatus = "Approved";
                    user.IsLocked = false;
                    user.FailedAttempts = 0;
                    approvedCount++;
                    approvedUsers.Add(user.UserName ?? user.UserLoginId);
                }

                await _context.SaveChangesAsync();

                _logger.LogInformation($"{approvedCount} users bulk approved by {User.Identity?.Name}");

                return Json(new
                {
                    success = true,
                    message = $"✅ Successfully approved {approvedCount} user(s): {string.Join(", ", approvedUsers)}",
                    approvedCount = approvedCount,
                    approvedUsers = approvedUsers
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in bulk user approval");
                return Json(new
                {
                    success = false,
                    message = $"Error in bulk approval: {ex.Message}"
                });
            }
        }




        // ============================================================
        // GET: /Account/PendingApprovals
        // ============================================================
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> PendingApprovals(string searchTerm)
        {
            try
            {
                var currentUserRole = User.FindFirstValue(ClaimTypes.Role);
                var currentUserGroup = User.FindFirstValue("UserGroup");

                // Only Super Admin can access this
                bool isSuperAdmin = currentUserRole == "Super Admin" ||
                                    currentUserGroup == "Super Admin" ||
                                    currentUserRole == "SuperAdmin";

                if (!isSuperAdmin)
                {
                    TempData["ErrorMessage"] = "Access denied. Only Super Administrators can approve users.";
                    return RedirectToAction("Index", "Home");
                }

                ViewBag.SearchTerm = searchTerm;

                // Get all pending users (Status = "Pending" or ApprovalStatus = "Pending")
                var query = _context.UserAccounts1
                    .Where(u => u.Status == "Pending" || u.ApprovalStatus == "Pending")
                    .AsQueryable();

                if (!string.IsNullOrEmpty(searchTerm))
                {
                    query = query.Where(u => u.UserName.Contains(searchTerm) ||
                                              u.Email.Contains(searchTerm) ||
                                              u.Phone.Contains(searchTerm) ||
                                              u.UserLoginId.Contains(searchTerm));
                }

                var pendingUsers = await query
                    .OrderByDescending(u => u.DateCreated)
                    .Select(u => new PendingUserDTO
                    {
                        UserId = u.UserId,
                        UserName = u.UserName ?? "Unknown",
                        UserLoginId = u.UserLoginId ?? "",
                        Email = u.Email ?? "",
                        Phone = u.Phone ?? "",
                        Department = u.Department ?? "",
                        UserGroup = u.UserGroup ?? "Member",
                        CompanyCode = u.CompanyCode ?? "",
                        CompanyName = _context.Companies
                            .Where(c => c.CompanyCode == u.CompanyCode)
                            .Select(c => c.CompanyName)
                            .FirstOrDefault() ?? "Unknown",
                        DateCreated = u.DateCreated ?? DateTime.Now,
                        SubCounty = u.SubCounty ?? "",
                        Ward = u.Ward ?? ""
                    })
                    .ToListAsync();

                ViewBag.PendingCount = pendingUsers.Count;
                return View(pendingUsers);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading pending approvals");
                TempData["ErrorMessage"] = "Error loading pending approvals";
                return View(new List<PendingUserDTO>());
            }
        }

        // GET: /Account/LockedUsers
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> LockedUsers(string searchTerm)
        {
            try
            {
                var currentUserRole = User.FindFirstValue(ClaimTypes.Role);
                var currentUserGroup = User.FindFirstValue("UserGroup");

                // Only Super Admin can view locked users
                bool isSuperAdmin = currentUserRole == "Super Admin" || currentUserGroup == "Super Admin";

                if (!isSuperAdmin)
                {
                    TempData["ErrorMessage"] = "Access denied. Only Super Administrators can view locked users.";
                    return RedirectToAction("Index", "Home");
                }

                ViewBag.SearchTerm = searchTerm;

                var query = _context.UserAccounts1
                    .Where(u => u.IsLocked == true)
                    .AsQueryable();

                if (!string.IsNullOrEmpty(searchTerm))
                {
                    query = query.Where(u => u.UserName.Contains(searchTerm) ||
                                              u.Email.Contains(searchTerm) ||
                                              u.Phone.Contains(searchTerm));
                }

                var lockedUsers = await query
                    .OrderByDescending(u => u.DateCreated)
                    .Select(u => new LockedUserDTO
                    {
                        UserId = u.UserId,
                        UserName = u.UserName,
                        UserLoginId = u.UserLoginId,
                        Email = u.Email ?? "",
                        Phone = u.Phone ?? "",
                        Department = u.Department ?? "",
                        UserGroup = u.UserGroup ?? "Member",
                        CompanyCode = u.CompanyCode ?? "",
                        CompanyName = _context.Companies
                            .Where(c => c.CompanyCode == u.CompanyCode)
                            .Select(c => c.CompanyName)
                            .FirstOrDefault() ?? "Unknown",
                        FailedAttempts = u.FailedAttempts ?? 0,
                        //LockedDate = u.ModifiedAt ?? u.DateCreated,
                        LockReason = u.FailedAttempts >= 5 ? "Too many failed login attempts" : "Manually locked"
                    })
                    .ToListAsync();

                ViewBag.LockedCount = lockedUsers.Count;
                return View(lockedUsers);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading locked users");
                TempData["ErrorMessage"] = "Error loading locked users";
                return View(new List<LockedUserDTO>());
            }
        }

        // POST: /Account/UnlockUser (Updated for Super Admin only)
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UnlockUser(int userId)
        {
            try
            {
                var currentUserRole = User.FindFirstValue(ClaimTypes.Role);
                var currentUserGroup = User.FindFirstValue("UserGroup");

                // Only Super Admin can unlock users
                bool isSuperAdmin = currentUserRole == "Super Admin" || currentUserGroup == "Super Admin";

                if (!isSuperAdmin)
                {
                    return Json(new { success = false, message = "Access denied. Only Super Administrators can unlock users." });
                }

                var user = await _context.UserAccounts1.FindAsync(userId);
                if (user == null)
                {
                    return Json(new { success = false, message = "User not found" });
                }

                // Unlock the user
                user.IsLocked = false;
                user.FailedAttempts = 0;

                // If status was Locked, set to Active
                if (user.Status == "Locked")
                {
                    user.Status = "Active";
                    user.Userstatus = "Active";
                }

                // Record unlock in blockchain
                var blockchainData = new
                {
                    Action = "USER_UNLOCKED",
                    UserId = user.UserId,
                    UserName = user.UserName,
                    UnlockedBy = User.Identity?.Name,
                    UnlockedAt = DateTime.Now
                };

                var blockchainTx = new BlockchainTransaction
                {
                    TransactionId = Guid.NewGuid().ToString(),
                    TransactionType = "USER_UNLOCKED",
                    MemberNo = user.MemberNo,
                    CompanyCode = user.CompanyCode,
                    Amount = 0,
                    Timestamp = DateTime.Now,
                    DataHash = await HashBlockchainDataAsync(blockchainData),
                    PayloadJson = JsonSerializer.Serialize(blockchainData),
                    OffChainReferenceId = user.UserId.ToString(),
                    Status = "CONFIRMED",
                    CreatedAt = DateTime.Now
                };

                _context.BlockchainTransactions.Add(blockchainTx);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"User {user.UserName} (ID: {user.UserId}) unlocked by {User.Identity?.Name}");

                return Json(new { success = true, message = $"User '{user.UserName}' has been unlocked successfully." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error unlocking user {userId}");
                return Json(new { success = false, message = $"Error unlocking user: {ex.Message}" });
            }
        }

        // POST: /Account/BulkApproveUsers
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BulkApproveUsers(int[] userIds)
        {
            try
            {
                var currentUserRole = User.FindFirstValue(ClaimTypes.Role);
                var currentUserGroup = User.FindFirstValue("UserGroup");

                bool isSuperAdmin = currentUserRole == "Super Admin" || currentUserGroup == "Super Admin";

                if (!isSuperAdmin)
                {
                    return Json(new { success = false, message = "Access denied. Only Super Administrators can approve users." });
                }

                if (userIds == null || userIds.Length == 0)
                {
                    return Json(new { success = false, message = "No users selected for approval." });
                }

                var approvedCount = 0;
                var errors = new List<string>();

                foreach (var userId in userIds)
                {
                    var user = await _context.UserAccounts1.FindAsync(userId);
                    if (user != null && (user.Status == "Pending" || user.ApprovalStatus == "Pending"))
                    {
                        user.Status = "Active";
                        user.Userstatus = "Active";
                        user.ApprovalStatus = "Approved";
                        user.IsLocked = false;
                        user.FailedAttempts = 0;
                        approvedCount++;
                    }
                    else if (user != null)
                    {
                        errors.Add($"User '{user.UserName}' is not pending approval.");
                    }
                }

                await _context.SaveChangesAsync();

                var message = $"{approvedCount} user(s) approved successfully.";
                if (errors.Any())
                {
                    message += $" Errors: {string.Join("; ", errors)}";
                }

                _logger.LogInformation($"Bulk approval: {approvedCount} users approved by {User.Identity?.Name}");

                return Json(new { success = true, message = message, approvedCount = approvedCount });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in bulk approval");
                return Json(new { success = false, message = $"Error: {ex.Message}" });
            }
        }

        // Helper method for hashing blockchain data
        private async Task<string> HashBlockchainDataAsync(object data)
        {
            var json = JsonSerializer.Serialize(data);
            using var sha256 = SHA256.Create();
            var hashedBytes = await Task.Run(() => sha256.ComputeHash(Encoding.UTF8.GetBytes(json)));
            return Convert.ToBase64String(hashedBytes);
        }



        // ============================================================
        // LOGIN OTP VERIFICATION - Auto-verification on OTP entry
        // ============================================================

        [HttpGet]
        [Authorize]
        public async Task<IActionResult> LoginOTPVerification(string returnUrl = null)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var userName = User.Identity?.Name;
            var email = User.FindFirstValue("Email");

            _logger.LogInformation($"LoginOTPVerification GET: UserId={userId}, ReturnUrl={returnUrl}");

            // Check if user already has a valid OTP
            if (_otpService.HasValidOtp(userId))
            {
                _logger.LogInformation($"User {userId} already has valid OTP.");

                HttpContext.Session.SetString($"LoginOtpValidated_{userId}", "true");
                HttpContext.Session.SetString($"LoginOtpValidatedAt_{userId}", DateTime.UtcNow.ToString("O"));

                if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                {
                    return Redirect(returnUrl);
                }
                return RedirectToAction("Index", "Home");
            }

            // Clear any existing OTP for this user
            _otpService.ClearOtp(userId);

            // Generate new OTP
            var otp = _otpService.GenerateOtp(userId);
            var generatedOtpExpiry = _otpService.GetOtpExpiry(userId);

            _logger.LogInformation($"Generated new Login OTP for user {userId}. OTP: {otp}");

            // Send OTP via email
            var emailService = HttpContext.RequestServices.GetService<IEmailService>();
            string emailSentMessage = null;

            if (emailService != null)
            {
                var user = await _context.UserAccounts1.FirstOrDefaultAsync(u => u.UserId.ToString() == userId);
                if (user != null && !string.IsNullOrEmpty(user.Email))
                {
                    string maskedEmail = MaskEmail(user.Email);
                    var emailSent = await emailService.SendOtpAsync(user.Email, user.UserName, otp, 5);

                    if (emailSent)
                    {
                        _logger.LogInformation($"Login OTP sent to {user.Email} for user {user.UserName}");
                        emailSentMessage = $"✅ OTP has been sent to {maskedEmail}";
                        TempData["EmailSentMessage"] = emailSentMessage;
                    }
                    else
                    {
                        _logger.LogWarning($"Failed to send Login OTP email to {user.Email}");
                        emailSentMessage = "⚠️ Failed to send OTP email. Please try again or contact support.";
                        TempData["ErrorMessage"] = emailSentMessage;
                    }
                }
                else
                {
                    TempData["ErrorMessage"] = "No email address associated with this account. Please contact administrator.";
                }
            }
            else
            {
                // For development/testing - show code on screen
                _logger.LogWarning($"Email service not configured. Login OTP for {userId}: {otp}");
                TempData["EmailSentMessage"] = $"[DEV MODE] Login OTP: {otp}";
            }

            var remainingSeconds = _otpService.GetRemainingSeconds(userId);
            if (remainingSeconds <= 0)
            {
                remainingSeconds = 300;
            }

            var model = new OtpVerificationViewModel
            {
                ReturnUrl = returnUrl,
                UserId = userId,
                UserName = userName,
                Email = email,
                OtpExpiry = generatedOtpExpiry,
                IsOtpValid = false,
                RemainingSeconds = remainingSeconds
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ValidateLoginOtp(OtpVerificationViewModel model)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            _logger.LogInformation($"=== ValidateLoginOtp START ===");
            _logger.LogInformation($"UserId: {userId}");
            _logger.LogInformation($"Otp: {model.Otp}");
            _logger.LogInformation($"IsAjaxRequest: {Request.Headers["X-Requested-With"] == "XMLHttpRequest"}");

            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogWarning("ValidateLoginOtp: UserId is null or empty");
                TempData["ErrorMessage"] = "User not authenticated. Please login again.";

                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return Json(new { success = false, message = "User not authenticated" });
                }
                return RedirectToAction("Login", "Account");
            }

            // Validate the OTP
            bool isValid = _otpService.ValidateOtp(userId, model.Otp);

            _logger.LogInformation($"ValidateLoginOtp: Validation result = {isValid} for user {userId}");

            if (isValid)
            {
                // Store OTP validation in session
                HttpContext.Session.SetString($"LoginOtpValidated_{userId}", "true");
                HttpContext.Session.SetString($"LoginOtpValidatedAt_{userId}", DateTime.UtcNow.ToString("O"));

                _logger.LogInformation($"ValidateLoginOtp: OTP validated successfully for user {userId}");
                TempData["SuccessMessage"] = "OTP validated successfully. Welcome!";

                // Clear the OTP after successful validation
                _otpService.ClearOtp(userId);

                // Determine redirect URL
                string redirectUrl;
                if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
                {
                    redirectUrl = model.ReturnUrl;
                }
                else
                {
                    redirectUrl = Url.Action("Index", "Home");
                }

                // For AJAX requests, return JSON with redirect URL
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return Json(new
                    {
                        success = true,
                        redirectUrl = redirectUrl,
                        message = "OTP validated successfully!"
                    });
                }

                // For normal form submissions, redirect directly
                return Redirect(redirectUrl);
            }

            // OTP validation failed
            _logger.LogWarning($"ValidateLoginOtp: OTP validation failed for user {userId}");

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return Json(new
                {
                    success = false,
                    message = "Invalid OTP. Please try again.",
                    remainingSeconds = _otpService.GetRemainingSeconds(userId)
                });
            }

            ModelState.AddModelError("Otp", "Invalid OTP. Please try again.");
            model.RemainingSeconds = _otpService.GetRemainingSeconds(userId);
            model.OtpExpiry = _otpService.GetOtpExpiry(userId);

            return View("LoginOTPVerification", model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResendLoginOtp(string userId)
        {
            try
            {
                _logger.LogInformation($"ResendLoginOtp: UserId={userId}");

                if (string.IsNullOrEmpty(userId))
                {
                    return Json(new { success = false, message = "User ID is required." });
                }

                var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (userId != currentUserId)
                {
                    _logger.LogWarning($"ResendLoginOtp: Unauthorized access. UserId={userId}, CurrentUserId={currentUserId}");
                    return Json(new { success = false, message = "Unauthorized access." });
                }

                // Clear existing OTP
                _otpService.ClearOtp(userId);

                // Generate new OTP
                var otp = _otpService.GenerateOtp(userId);
                var resendOtpExpiry = _otpService.GetOtpExpiry(userId);

                _logger.LogInformation($"ResendLoginOtp: New OTP generated for user {userId}. OTP: {otp}, Expiry: {resendOtpExpiry:HH:mm:ss}");

                // Send OTP via email
                var emailService = HttpContext.RequestServices.GetService<IEmailService>();
                var user = await _context.UserAccounts1.FirstOrDefaultAsync(u => u.UserId.ToString() == userId);

                if (user == null)
                {
                    return Json(new { success = false, message = "User not found." });
                }

                if (string.IsNullOrEmpty(user.Email))
                {
                    return Json(new { success = false, message = "No email address associated with this account." });
                }

                var emailSent = await emailService.SendOtpAsync(user.Email, user.UserName, otp, 5);

                if (emailSent)
                {
                    var remainingSeconds = _otpService.GetRemainingSeconds(userId);
                    _logger.LogInformation($"ResendLoginOtp: RemainingSeconds={remainingSeconds}");

                    return Json(new
                    {
                        success = true,
                        message = "New OTP has been sent to your email.",
                        expiry = resendOtpExpiry,
                        remainingSeconds = remainingSeconds
                    });
                }
                else
                {
                    return Json(new
                    {
                        success = false,
                        message = "Failed to send OTP. Please try again or contact administrator."
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error resending Login OTP for user {userId}");
                return Json(new { success = false, message = "An error occurred. Please try again." });
            }
        }


        // ============================================================
        // POST: /Account/ResendResetCode
        // Resends the password-reset OTP for an anonymous user.
        // ============================================================
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResendResetCode(string username)
        {
            try
            {
                _logger.LogInformation($"ResendResetCode: Username={username}");

                if (string.IsNullOrWhiteSpace(username))
                {
                    return Json(new { success = false, message = "Username is required." });
                }

                // ============================================================
                // SERVER-SIDE COOLDOWN (5 minutes per username per session)
                // Prevents abuse even if client-side checks are bypassed.
                // ============================================================
                const int CooldownSeconds = 300; // 5 minutes

                var lastSentKey = $"ResetCodeLastSent_{username}";
                var lastSentStr = HttpContext.Session.GetString(lastSentKey);

                if (!string.IsNullOrEmpty(lastSentStr) &&
                    DateTime.TryParse(lastSentStr, null,
                        System.Globalization.DateTimeStyles.RoundtripKind,
                        out var lastSent))
                {
                    // Normalize to UTC to avoid timezone drift
                    lastSent = lastSent.ToUniversalTime();
                    var elapsed = DateTime.UtcNow - lastSent;

                    if (elapsed.TotalSeconds < CooldownSeconds)
                    {
                        var remainingSeconds = (int)Math.Ceiling(CooldownSeconds - elapsed.TotalSeconds);
                        if (remainingSeconds < 1) remainingSeconds = 1;

                        var remainingMinutes = Math.Ceiling(remainingSeconds / 60.0);
                        var minutesText = remainingMinutes == 1
                            ? "1 minute"
                            : $"{remainingMinutes:0} minute{(remainingMinutes == 1 ? "" : "s")}";

                        _logger.LogWarning(
                            $"ResendResetCode: Rate limit hit for {username}. Wait {remainingSeconds}s ({minutesText}).");

                        return Json(new
                        {
                            success = false,
                            message = $"Please wait {minutesText} before requesting another code.",
                            remainingSeconds = remainingSeconds,
                            remainingMinutes = remainingMinutes
                        });
                    }
                }

                // Look up the user
                var user = await _context.UserAccounts1
                    .FirstOrDefaultAsync(u => u.UserName == username);

                // For security, don't reveal whether the user exists.
                if (user == null)
                {
                    // Return success to avoid user enumeration, but don't send anything.
                    return Json(new
                    {
                        success = true,
                        message = "If the account exists, a new verification code has been sent.",
                        remainingSeconds = CooldownSeconds,
                        remainingMinutes = CooldownSeconds / 60
                    });
                }

                if (string.IsNullOrEmpty(user.Email))
                {
                    return Json(new
                    {
                        success = false,
                        message = "No email address associated with this account. Please contact administrator."
                    });
                }

                if (user.Status?.ToLower() != "active" && user.Userstatus?.ToLower() != "active")
                {
                    return Json(new
                    {
                        success = false,
                        message = "Your account is not active. Please contact administrator."
                    });
                }

                // Generate a fresh 6-digit code (10-minute window, matches ForgotPassword)
                var verificationCode = GenerateVerificationCode();
                var codeExpiry = DateTime.Now.AddMinutes(10);

                // Store in the SAME session keys that VerifyCode POST reads
                HttpContext.Session.SetString($"ResetCode_{username}", verificationCode);
                HttpContext.Session.SetString($"ResetCodeExpiry_{username}", codeExpiry.ToString("O"));

                // Record timestamp for server-side cooldown (always UTC, roundtrip format)
                HttpContext.Session.SetString(lastSentKey, DateTime.UtcNow.ToString("O"));

                // Send using the password-reset email template
                var emailService = HttpContext.RequestServices.GetService<IEmailService>();
                if (emailService == null)
                {
                    _logger.LogWarning($"Email service not configured. Reset code for {username}: {verificationCode}");
                    return Json(new
                    {
                        success = true,
                        message = $"[DEV MODE] Verification code: {verificationCode}",
                        remainingSeconds = CooldownSeconds,
                        remainingMinutes = CooldownSeconds / 60
                    });
                }

                var emailSent = await emailService.SendVerificationCodeAsync(
                    user.Email, user.UserName, verificationCode);

                if (!emailSent)
                {
                    return Json(new
                    {
                        success = false,
                        message = "Failed to send verification email. Please try again later."
                    });
                }

                _logger.LogInformation(
                    $"ResendResetCode: New code sent to {MaskEmail(user.Email)} for user {username}");

                return Json(new
                {
                    success = true,
                    message = $"A new verification code has been sent to {MaskEmail(user.Email)}.",
                    remainingSeconds = CooldownSeconds,
                    remainingMinutes = CooldownSeconds / 60
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error resending reset code for {username}");
                return Json(new { success = false, message = "An error occurred. Please try again." });
            }
        }
    }
}