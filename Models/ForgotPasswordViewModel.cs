using System.ComponentModel.DataAnnotations;

namespace Authenticate.Models
{
    public class ForgotPasswordViewModel
    {
        [Required, EmailAddress]
        public string Email { get; set; } = string.Empty;
    }
}