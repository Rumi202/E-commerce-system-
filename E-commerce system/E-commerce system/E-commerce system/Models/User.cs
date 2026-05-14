using System.ComponentModel.DataAnnotations;

namespace E_commerce_system.Models
{
    public class User
    {
        [Key]
        public int Id { get; set; }
        
        [Required]
        public string Name { get; set; }
        
        [Required]
        [EmailAddress]
        public string Email { get; set; }
        
        public string PasswordHash { get; set; }
        public string Role { get; set; } // "Admin" or "User"
        public bool IsBlocked { get; set; }
        
        public string? Address { get; set; }
        public string? ProfilePictureUrl { get; set; }

        public bool IsEmailVerified { get; set; } = false;
        public string? EmailVerificationToken { get; set; }
    }
}
