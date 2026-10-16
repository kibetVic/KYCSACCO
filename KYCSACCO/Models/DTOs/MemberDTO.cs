using System;
using System.ComponentModel.DataAnnotations;

namespace KYCSACCO.Models.DTOs
{
    // ============================================================
    // REGISTRATION DTO — used by Create (POST)
    // CompanyCode and Cigcode are both required. CompanyCode is set
    // from the CIG dropdown on the form.
    // ============================================================
    public class MemberRegistrationDTO
    {
        [Display(Name = "Member Number")]
        public string? MemberNo { get; set; }

        [Required(ErrorMessage = "Surname is required")]
        [Display(Name = "Surname")]
        public string Surname { get; set; } = null!;

        [Required(ErrorMessage = "Other names are required")]
        [Display(Name = "Other Names")]
        public string OtherNames { get; set; } = null!;

        [Required(ErrorMessage = "ID Number is required")]
        [Display(Name = "ID Number")]
        public string IdNo { get; set; } = null!;

        [Phone(ErrorMessage = "Invalid phone number")]
        [Display(Name = "Phone Number")]
        public string? PhoneNo { get; set; }

        [Phone(ErrorMessage = "Invalid landline number")]
        [Display(Name = "Landline")]
        public string? LandLine { get; set; }

        [EmailAddress(ErrorMessage = "Invalid email address")]
        [Display(Name = "Email Address")]
        public string? Email { get; set; }

        [Display(Name = "Gender")]
        public string? Gender { get; set; }

        [Display(Name = "Date of Birth")]
        [DataType(DataType.Date)]
        public DateTime? DateOfBirth { get; set; }

        [Display(Name = "Age")]
        public int? Age { get; set; }

        [Display(Name = "Station/County")]
        public string? Station { get; set; }

        [Display(Name = "Department")]
        public string? Department { get; set; }

        [Display(Name = "Present Address")]
        public string? PresentAddress { get; set; }

        // ============================================================
        // CompanyCode — required, comes from the selected CIG on the form.
        // ============================================================
        [Required(ErrorMessage = "Company Code is required (select a CIG first)")]
        [Display(Name = "Company Code")]
        public string? CompanyCode { get; set; }

        // ============================================================
        // Cigcode — required; used to look up the company code.
        // ============================================================
        [Required(ErrorMessage = "Please select a Group CIG.")]
        [Display(Name = "Group CIG")]
        public string? Cigcode { get; set; }

        [Display(Name = "Membership Type")]
        public string? MembershipType { get; set; }

        [Display(Name = "Registration Type")]
        public string? RegistrationType { get; set; }

        [Display(Name = "Initial Shares")]
        [Range(0, double.MaxValue, ErrorMessage = "Initial shares must be a positive number")]
        public decimal InitialShares { get; set; } = 0;

        public string? Photo { get; set; }
        public string? IdFrontImage { get; set; }
        public string? IdBackImage { get; set; }
        public string? CreatedBy { get; set; }
        public string? MaritalStatus { get; set; }
        public string? Status { get; set; }
        public DateTime RegistrationDate { get; set; } = DateTime.Now;
        public string? Employer { get; set; }
    }

    // ============================================================
    // UPDATE DTO — used by Update (POST)
    // ============================================================
    public class MemberUpdateDTO
    {
        public string? MemberNo { get; set; }
        public string? PhoneNo { get; set; }
        public string? IdNo { get; set; }
        public string? CompanyCode { get; set; }
        public string? Surname { get; set; }
        public string? OtherNames { get; set; }
        public string? Gender { get; set; }
        public DateTime? DateOfBirth { get; set; }
        public string? Age { get; set; }
        public string? Cigcode { get; set; }
        public string? LandLine { get; set; }
        public string? Email { get; set; }
        public string? PresentAddress { get; set; }
        public string? HomeAddress { get; set; }
        public string? Employer { get; set; }
        public string? Department { get; set; }
        public string? Station { get; set; }
        public string? MembershipType { get; set; }
        public string? RegistrationType { get; set; }
        public string? MaritalStatus { get; set; }
        public string? Photo { get; set; }
        public string? IdFrontImage { get; set; }
        public string? IdBackImage { get; set; }
        public string? GroupCig { get; set; }
        public string? UpdatedBy { get; set; }
        public string? CreatedBy { get; set; }
        public string? Status { get; set; }
        public DateTime RegistrationDate { get; set; }
    }

    // ============================================================
    // RESPONSE DTO
    // ============================================================
    public class MemberResponseDTO
    {
        public string MemberNo { get; set; } = null!;
        public string FullName { get; set; } = null!;
        public string Status { get; set; } = null!;
        public DateTime RegistrationDate { get; set; }
        public string? BlockchainTxId { get; set; }
        public decimal ShareBalance { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string? CompanyCode { get; set; }
        public string? MembershipType { get; set; }
        public string? RegistrationType { get; set; }
        public bool IsActive { get; set; }
        public string? Employer { get; set; }
        public string? Photo { get; set; }
        public string? IdFrontImage { get; set; }
        public string? IdBackImage { get; set; }
    }

    // ============================================================
    // Legacy DTO kept for compatibility with other parts of the system.
    // ============================================================
    public class MemberDTO
    {
        public int Id { get; set; }
        public string MemberNo { get; set; } = null!;
        public string Surname { get; set; } = null!;
        public string OtherNames { get; set; } = null!;
        public string FullName { get; set; } = null!;
        public string Idno { get; set; } = null!;
        public string? PhoneNo { get; set; }
        public string? LandLine { get; set; }
        public string? Email { get; set; }
        public string? OfficePhone { get; set; }
        public string? HomePhone { get; set; }
        public string? Gender { get; set; }
        public DateTime? DateOfBirth { get; set; }
        public int? Age { get; set; }
        public string? MaritalStatus { get; set; }
        public string? Employer { get; set; }
        public string? Department { get; set; }
        public string? Station { get; set; }
        public string? PresentAddress { get; set; }
        public string? HomeAddress { get; set; }
        public string CompanyCode { get; set; } = null!;
        public string? GroupCig { get; set; }
        public string? MembershipType { get; set; }
        public string? RegistrationType { get; set; }
        public string Status { get; set; } = null!;
        public DateTime? DateJoined { get; set; }
        public decimal CurrentBalance { get; set; }
        public decimal ShareBalance { get; set; }
        public decimal LoanBalance { get; set; }
        public decimal TotalBalance { get; set; }
        public bool IsActive { get; set; }
        public bool IsDormant { get; set; }
        public string? IdFrontImage { get; set; }
        public string? IdBackImage { get; set; }
        public DateTime? LastTransactionDate { get; set; }
        public string? ProfilePicture { get; set; }
        public string? BlockchainTxId { get; set; }
        public int TotalTransactions { get; set; }
    }
}