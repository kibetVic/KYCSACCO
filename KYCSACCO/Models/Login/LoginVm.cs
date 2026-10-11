// Models/ViewModels/LoginVm.cs
using System.ComponentModel.DataAnnotations;

namespace KYCSACCO.Models.ViewModels
{
    public class LoginVm
    {
        [Required(ErrorMessage = "Username is required")]
        [Display(Name = "Username")]
        public string? Username { get; set; }

        [Required(ErrorMessage = "Password is required")]
        [DataType(DataType.Password)]
        public string? Password { get; set; }

        [Display(Name = "Remember me")]
        public bool RememberMe { get; set; }

        public string MemberLogin { get; set; } = "false";
    }
}