using System.ComponentModel.DataAnnotations;

namespace UserManagement.Core.Entities
{
    public class User
    {
        public int Id { get; set; }
        [Required]
        [StringLength(50)]
        public string FirstName { get; set; }

        [Required]
        [StringLength(50)]
        public string LastName { get; set; }

        [Required]
        [EmailAddress]
        public string Email { get; set; }

        [Required]
        [Phone]
        public string PhoneNumber { get; set; }

        [Required]
        [MinLength(8)]
        public string PasswordHash { get; set; }
        public bool IsEmailVerified { get; set; }
        public string VerificationCode { get; set; }
        public DateTime? VerificationCodeExpiration { get; set; }
    }
}
