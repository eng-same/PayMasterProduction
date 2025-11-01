using System.ComponentModel.DataAnnotations;

namespace Application.PL.ViewModels
{
    public class VisitorRequestViewModel
    {
        [Required, StringLength(250)]
        public string CompanyName { get; set; }

        [Required, StringLength(200)]
        public string ContactName { get; set; }

        [Required, EmailAddress, StringLength(200)]
        public string Email { get; set; }

        [StringLength(50)]
        public string Phone { get; set; }

        [Range(1, 500)]
        public int NumberOfEmployees { get; set; } = 1;

        [StringLength(2000)]
        public string Message { get; set; }

        [Required, StringLength(200, MinimumLength = 6)]
        public string Password { get; set; }
    }
}