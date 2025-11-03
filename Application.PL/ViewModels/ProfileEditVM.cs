using System.ComponentModel.DataAnnotations;

namespace Application.PL.ViewModels
{
    public class ProfileEditVM
    {
        public string Id { get; set; } = null!;

        [Required, MaxLength(100)]
        public string? FirstName { get; set; }

        [Required, MaxLength(100)]
        public string? LastName { get; set; }

        [Required, EmailAddress]
        public string? Email { get; set; }

        [Required]
        public string? UserName { get; set; } // kept for display/sync with email

        [Phone]
        public string? PhoneNumber { get; set; }

        // existing pic path (read-only on the form)
        public string? ProfilePic { get; set; }

        // uploaded file for new profile pic
        public IFormFile? ProfileImageFile { get; set; }
    }
}
