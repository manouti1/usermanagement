using System.ComponentModel.DataAnnotations;

namespace UserManagement.DTOs
{
    public class VerifyEmailDto
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; }
    }
}
