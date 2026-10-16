using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using KYCSACCO.Data;
using KYCSACCO.Models;
using KYCSACCO.Models.DTOs;
using KYCSACCO.Models.ViewModels;
using KYCSACCO.Services;
using System.Security.Claims;
using System.Text.Json;

namespace KYCSACCO.Controllers
{
    [Authorize]
    public class MemberMvcController : Controller
    {
        private readonly IMemberService _memberService;
        private readonly ApplicationDbContext _context;
        private readonly IContributionService _contributionService;
        private readonly AuditTrailService _auditService;
        private readonly ICompanyContextService _companyContextService;
        private readonly ILogger<MemberMvcController> _logger;

        public MemberMvcController(
            IMemberService memberService,
            ApplicationDbContext context,
            IContributionService contributionService,
            ICompanyContextService companyContextService,
            ILogger<MemberMvcController> logger,
            AuditTrailService auditService)
        {
            _memberService = memberService;
            _context = context;
            _contributionService = contributionService;
            _companyContextService = companyContextService;
            _auditService = auditService;
            _logger = logger;
        }

        // ============================================================
        // INDEX — show ALL members whose company is a KYC company.
        // Not filtered by the logged-in user's company.
        // ============================================================
        public async Task<IActionResult> Index()
        {
            try
            {
                var membersQuery = _context.Members
                    .AsNoTracking()
                    .Where(m => m.CompanyCode.StartsWith("KYC")
                             && (m.Archived == false || m.Archived == null));

                var totalMembers = await membersQuery.CountAsync();
                var activeMembers = await membersQuery.CountAsync(m => m.Status == 1);
                var blockchainVerifiedCount = await membersQuery
                    .CountAsync(m => m.BlockchainTxId != null && m.BlockchainTxId != "");
                var totalShareCapital = await membersQuery.SumAsync(m => m.ShareCap ?? 0);

                var recentMembers = await membersQuery
                    .OrderByDescending(m => m.AuditDateTime)
                    .Take(20)
                    .Select(m => new Member
                    {
                        MemberNo = m.MemberNo,
                        Surname = m.Surname,
                        OtherNames = m.OtherNames,
                        Idno = m.Idno,
                        PhoneNo = m.PhoneNo,
                        Email = m.Email,
                        HomeTelNo = m.HomeTelNo,
                        Sex = m.Sex,
                        Dob = m.Dob,
                        Age = m.Age,
                        Mstatus = m.Mstatus,
                        Station = m.Station,
                        Dept = m.Dept,
                        PresentAddr = m.PresentAddr,
                        Cigcode = m.Cigcode,
                        MembershipType = m.MembershipType,
                        MemberDescription = m.MemberDescription,
                        Status = m.Status,
                        ApplicDate = m.ApplicDate,
                        AuditDateTime = m.AuditDateTime,
                        InitShares = m.InitShares,
                        ShareCap = m.ShareCap,
                        CompanyCode = m.CompanyCode,
                        BlockchainTxId = m.BlockchainTxId
                    })
                    .ToListAsync();

                // KYC CIGs for the dropdown
                var cigs = await _context.CIGs
                    .AsNoTracking()
                    .Where(c => c.CompanyCode.StartsWith("KYC") && c.Status == "Active")
                    .OrderBy(c => c.CompanyCode)
                    .ThenBy(c => c.GigName)
                    .Select(c => new { c.GigCode, c.GigName, c.CompanyCode })
                    .ToListAsync();

                var counties = await _context.Counties
                    .AsNoTracking()
                    .Where(c => c.Status == "Active")
                    .OrderBy(c => c.CountyName)
                    .Select(c => new { c.CountyName })
                    .ToListAsync();

                ViewBag.CIGs = cigs;
                ViewBag.Counties = counties;
                ViewBag.TotalMembersCount = totalMembers;

                var viewModel = new MembersIndexViewModel
                {
                    AllMembers = recentMembers,
                    TotalMembers = totalMembers,
                    ActiveMembers = activeMembers,
                    TotalShareCapital = totalShareCapital,
                    BlockchainVerifiedCount = blockchainVerifiedCount,
                    UserCompanyCode = null
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading members index");
                TempData["ErrorMessage"] = "Error loading members list: " + ex.Message;

                var emptyViewModel = new MembersIndexViewModel
                {
                    AllMembers = new List<Member>(),
                    TotalMembers = 0,
                    ActiveMembers = 0,
                    TotalShareCapital = 0,
                    BlockchainVerifiedCount = 0,
                    UserCompanyCode = null
                };

                return View(emptyViewModel);
            }
        }

        // ============================================================
        // SEARCH
        // ============================================================
        [HttpGet]
        public async Task<IActionResult> SearchMembers(string searchTerm, string statusFilter = "all")
        {
            try
            {
                var query = _context.Members
                    .Where(m => m.CompanyCode.StartsWith("KYC")
                             && (m.Archived == false || m.Archived == null))
                    .AsNoTracking();

                if (!string.IsNullOrWhiteSpace(searchTerm))
                {
                    searchTerm = searchTerm.Trim().ToLower();
                    query = query.Where(m =>
                        m.MemberNo.ToLower().Contains(searchTerm) ||
                        m.Surname.ToLower().Contains(searchTerm) ||
                        m.OtherNames.ToLower().Contains(searchTerm) ||
                        (m.Surname + " " + m.OtherNames).ToLower().Contains(searchTerm) ||
                        m.Idno.ToLower().Contains(searchTerm) ||
                        m.PhoneNo.ToLower().Contains(searchTerm) ||
                        (m.Email != null && m.Email.ToLower().Contains(searchTerm)));
                }

                if (statusFilter == "active")
                    query = query.Where(m => m.Status == 1);
                else if (statusFilter == "inactive")
                    query = query.Where(m => m.Status != 1);

                var members = await query
                    .OrderByDescending(m => m.ApplicDate)
                    .Select(m => new
                    {
                        memberNo = m.MemberNo,
                        fullName = (m.Surname + " " + m.OtherNames).Trim(),
                        surname = m.Surname,
                        otherNames = m.OtherNames,
                        idNo = m.Idno,
                        phoneNo = m.PhoneNo,
                        email = m.Email,
                        landLine = m.HomeTelNo,
                        gender = m.Sex,
                        dateOfBirth = m.Dob.HasValue ? m.Dob.Value.ToString("yyyy-MM-dd") : "",
                        age = m.Age,
                        maritalStatus = m.Mstatus == true ? "Married" : m.Mstatus == false ? "Single" : "",
                        station = m.Station,
                        department = m.Dept,
                        presentAddress = m.PresentAddr,
                        cigcode = m.Cigcode,
                        companyCode = m.CompanyCode,
                        membershipType = m.MembershipType,
                        registrationType = m.MemberDescription,
                        status = m.Status,
                        statusText = m.Status == 1 ? "Active" : "Inactive",
                        registrationDate = m.ApplicDate.HasValue ? m.ApplicDate.Value.ToString("yyyy-MM-dd") : "",
                        initialShares = m.InitShares,
                        photo = m.Photo,
                        idFrontImage = m.IdFrontImage,
                        idBackImage = m.IdBackImage
                    })
                    .ToListAsync();

                return Json(new { success = true, data = members, count = members.Count });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching members");
                return Json(new { success = false, message = ex.Message });
            }
        }

        // ============================================================
        // CREATE
        // ============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([FromBody] MemberRegistrationDTO model)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage)
                        .ToList();
                    return Json(new { success = false, message = string.Join(", ", errors) });
                }

                // -------- CIG → CompanyCode resolution --------
                if (string.IsNullOrWhiteSpace(model.Cigcode))
                    return Json(new { success = false, message = "Please select a CIG." });

                var cig = await _context.CIGs
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.GigCode == model.Cigcode);

                if (cig == null)
                    return Json(new { success = false, message = $"CIG '{model.Cigcode}' not found." });

                if (!cig.CompanyCode.StartsWith("KYC", StringComparison.OrdinalIgnoreCase))
                    return Json(new { success = false, message = "Selected CIG is not under a KYC company." });

                // Force the company code to match the CIG.
                model.CompanyCode = cig.CompanyCode;
                model.CreatedBy = User.Identity?.Name ?? "SYSTEM";

                // Age rule for Individual
                if (model.DateOfBirth.HasValue &&
                    (model.RegistrationType ?? "").Equals("Individual", StringComparison.OrdinalIgnoreCase))
                {
                    var age = CalculateAge(model.DateOfBirth.Value);
                    if (age < 18)
                        return Json(new { success = false, message = "Individual members must be 18 years or older." });
                }

                // Duplicate checks within the resolved company
                if (await _context.Members.AnyAsync(m => m.Idno == model.IdNo && m.CompanyCode == cig.CompanyCode))
                    return Json(new { success = false, message = $"ID Number '{model.IdNo}' is already registered." });

                if (!string.IsNullOrEmpty(model.PhoneNo) &&
                    await _context.Members.AnyAsync(m => m.PhoneNo == model.PhoneNo && m.CompanyCode == cig.CompanyCode))
                    return Json(new { success = false, message = $"Phone Number '{model.PhoneNo}' is already registered." });

                if (!string.IsNullOrEmpty(model.Email) &&
                    await _context.Members.AnyAsync(m => m.Email == model.Email && m.CompanyCode == cig.CompanyCode))
                    return Json(new { success = false, message = $"Email '{model.Email}' is already registered." });

                // Member number: use supplied or generate
                if (string.IsNullOrEmpty(model.MemberNo))
                    model.MemberNo = await GenerateUniqueMemberNumberAsync(cig.CompanyCode);
                else if (await _context.Members.AnyAsync(m => m.MemberNo == model.MemberNo && m.CompanyCode == cig.CompanyCode))
                    return Json(new { success = false, message = $"Member Number '{model.MemberNo}' already exists." });

                var memberResponse = await _memberService.RegisterMemberAsync(model);

                return Json(new
                {
                    success = true,
                    message = "Member registered successfully!",
                    member = memberResponse
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating member");
                return Json(new { success = false, message = ex.Message });
            }
        }

        // ============================================================
        // GET MEMBER (edit form loader)
        // ============================================================
        [HttpGet]
        [Route("MemberMvc/GetMember/{memberNo}")]
        [Route("MemberMvc/GetMember")]
        public async Task<IActionResult> GetMember(string memberNo)
        {
            try
            {
                if (string.IsNullOrEmpty(memberNo))
                    memberNo = Request.Query["memberNo"].ToString();

                if (string.IsNullOrEmpty(memberNo))
                    return Json(new { success = false, message = "Member number is required" });

                memberNo = memberNo.Trim();

                var member = await _context.Members
                    .FirstOrDefaultAsync(m => m.MemberNo == memberNo);

                if (member == null)
                    return Json(new { success = false, message = $"Member '{memberNo}' not found" });

                string statusText = member.Status switch
                {
                    1 => "Active",
                    2 => "Withdrawn",
                    3 => "Deceased",
                    4 => "Dormant",
                    5 => "Suspended",
                    _ => "Active"
                };

                string fullName = $"{member.Surname} {member.OtherNames}".Trim();

                return Json(new
                {
                    success = true,
                    member = new
                    {
                        MemberNo = member.MemberNo ?? "",
                        Surname = member.Surname ?? "",
                        OtherNames = member.OtherNames ?? "",
                        FullName = fullName,
                        IdNo = member.Idno ?? "",
                        PhoneNo = member.PhoneNo ?? member.MobileNo ?? "",
                        LandLine = member.HomeTelNo ?? member.OfficeTelNo ?? "",
                        Email = member.Email ?? member.EmailAddress ?? "",
                        Gender = member.Sex ?? "",
                        DateOfBirth = member.Dob?.ToString("yyyy-MM-dd") ?? "",
                        Age = member.Age?.ToString() ?? "",
                        MaritalStatus = member.Mstatus == true ? "Married" : member.Mstatus == false ? "Single" : "",
                        Employer = member.Employer ?? "",
                        Department = member.Dept ?? "",
                        Station = member.Station ?? "",
                        PresentAddress = member.PresentAddr ?? "",
                        HomeAddress = member.HomeAddr ?? "",
                        Cigcode = member.Cigcode ?? "",
                        GroupCig = member.Cigcode ?? "",
                        CompanyCode = member.CompanyCode ?? "",
                        MembershipType = member.MembershipType ?? "Individual",
                        RegistrationType = member.MemberDescription ?? "Ordinary Member",
                        Status = statusText,
                        RegistrationDate = member.ApplicDate?.ToString("yyyy-MM-dd") ?? DateTime.Now.ToString("yyyy-MM-dd"),
                        InitialShares = member.InitShares ?? 0,
                        ShareBalance = member.ShareCap ?? 0,
                        CurrentBalance = 0,
                        LoanBalance = member.LoanBalance ?? 0,
                        TotalBalance = (member.ShareCap ?? 0) - (member.LoanBalance ?? 0),
                        IsActive = member.Status == 1,
                        IsDormant = member.Dormant == 1,
                        BlockchainTxId = member.BlockchainTxId ?? "",
                        Photo = member.Photo ?? "",
                        IdFrontImage = member.IdFrontImage ?? "",
                        IdBackImage = member.IdBackImage ?? ""
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting member {MemberNo}", memberNo);
                return Json(new { success = false, message = $"Error: {ex.Message}" });
            }
        }

        // ============================================================
        // UPDATE
        // ============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Update([FromForm] MemberUpdateDTO model)
        {
            try
            {
                if (model == null)
                    return Json(new { success = false, message = "Model is NULL." });

                if (string.IsNullOrEmpty(model.MemberNo))
                    return Json(new { success = false, message = "Member number is required." });

                // If a CIG was supplied, resolve its company code.
                if (!string.IsNullOrWhiteSpace(model.Cigcode))
                {
                    var cig = await _context.CIGs
                        .AsNoTracking()
                        .FirstOrDefaultAsync(c => c.GigCode == model.Cigcode);

                    if (cig == null)
                        return Json(new { success = false, message = $"CIG '{model.Cigcode}' not found." });

                    if (!cig.CompanyCode.StartsWith("KYC", StringComparison.OrdinalIgnoreCase))
                        return Json(new { success = false, message = "Selected CIG is not under a KYC company." });

                    model.CompanyCode = cig.CompanyCode;
                }

                // Locate member by member number (company-agnostic).
                var existingMember = await _context.Members
                    .FirstOrDefaultAsync(m => m.MemberNo == model.MemberNo);

                if (existingMember == null)
                    return Json(new { success = false, message = "Member not found." });

                var result = await _memberService.UpdateMemberAsync(model.MemberNo, model);
                if (result == null)
                    return Json(new { success = false, message = "Member update failed." });

                return Json(new
                {
                    success = true,
                    message = "Member updated successfully!",
                    data = result
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating member");
                return Json(new { success = false, message = $"Error: {ex.Message}" });
            }
        }

        // ============================================================
        // DELETE (soft archive)
        // ============================================================
        [HttpDelete]
        public async Task<IActionResult> Delete(string memberNo)
        {
            try
            {
                var member = await _context.Members
                    .FirstOrDefaultAsync(m => m.MemberNo == memberNo);

                if (member == null)
                    return Json(new { success = false, message = "Member not found" });

                member.Archived = true;
                member.Status = 0;
                member.AuditTime = DateTime.Now;
                member.AuditId = User.Identity?.Name ?? "SYSTEM";

                await _context.SaveChangesAsync();

                await _auditService.SaveLogAsync(
                    actionType: AuditActionType.Delete,
                    oldModel: member,
                    tableName: "Members",
                    recordId: member.MemberNo,
                    userId: User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "SYSTEM",
                    userName: User.Identity?.Name ?? "SYSTEM",
                    companyCode: member.CompanyCode,
                    module: "MemberManagement"
                );

                return Json(new { success = true, message = "Member archived successfully!" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting member");
                return Json(new { success = false, message = ex.Message });
            }
        }

        // ============================================================
        // HELPERS
        // ============================================================
        private async Task<string> GenerateUniqueMemberNumberAsync(string companyCode)
        {
            string monthInitial = DateTime.Now.ToString("MMM").Substring(0, 1).ToUpper();
            string datePart = DateTime.Now.ToString("yyMMdd");
            string timePart = DateTime.Now.ToString("HHmmss");
            string combined = $"{datePart}{timePart}";
            string uniqueDigits = combined.Length > 11 ? combined.Substring(0, 11) : combined.PadLeft(11, '0');

            var lastMember = await _context.Members
                .Where(m => m.CompanyCode == companyCode && m.MemberNo.StartsWith(monthInitial))
                .OrderByDescending(m => m.MemberNo)
                .FirstOrDefaultAsync();

            if (lastMember != null && lastMember.MemberNo.Length >= 12)
            {
                string lastNumericPart = lastMember.MemberNo.Substring(1, 11);
                if (long.TryParse(lastNumericPart, out long lastNumber))
                {
                    long newNumber = lastNumber + 1;
                    uniqueDigits = newNumber.ToString().PadLeft(11, '0');
                    if (uniqueDigits.Length > 11) uniqueDigits = uniqueDigits.Substring(0, 11);
                }
            }

            string memberNo = $"{monthInitial}{uniqueDigits}";
            if (memberNo.Length > 12) memberNo = memberNo.Substring(0, 12);
            else if (memberNo.Length < 12) memberNo = memberNo.PadRight(12, '0');

            return memberNo;
        }

        private int CalculateAge(DateTime dateOfBirth)
        {
            var today = DateTime.Today;
            var age = today.Year - dateOfBirth.Year;
            if (dateOfBirth.Date > today.AddYears(-age)) age--;
            return age;
        }
    }
}