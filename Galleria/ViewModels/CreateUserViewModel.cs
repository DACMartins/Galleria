using System.ComponentModel.DataAnnotations;

namespace Galleria.ViewModels
{
    public class CreateUserViewModel
    {
        [Required]
        [EmailAddress]
        [Display(Name = "Email / Username")]
        public string Email { get; set; }

        [Required]
        [DataType(DataType.Password)]
        [MinLength(6, ErrorMessage = "Password must be at least 6 characters.")]
        public string Password { get; set; }

        [Required]
        [DataType(DataType.Password)]
        [Compare("Password", ErrorMessage = "Passwords do not match.")]
        [Display(Name = "Confirm Password")]
        public string ConfirmPassword { get; set; }

        // Se mais tarde quiseres escolher logo a role:
        // public string? RoleName { get; set; }
    }
}
