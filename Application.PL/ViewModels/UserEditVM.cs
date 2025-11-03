using System.ComponentModel.DataAnnotations;

namespace Application.PL.ViewModels
{
    public class UserEditVM
    {
        public string Id { get; set; } = null!;

        [Required, MaxLength(100)]
        public string? FirstName { get; set; }

        [Required, MaxLength(100)]
        public string? LastName { get; set; }

        [Required, EmailAddress]
        public string? Email { get; set; }

        [Required]
        public string? UserName { get; set; }

        public bool IsActive { get; set; }
        public string? ProfilePic { get; set; }

        // NEW: uploaded file
        public IFormFile? ProfileImageFile { get; set; }

        // Roles
        public List<string> AvailableRoles { get; set; } = new();
        public List<string> AssignedRoles { get; set; } = new();

        // Employee assignment
        public List<SelectEmployeeItem> EmployeeOptions { get; set; } = new();
        public int? SelectedEmployeeId { get; set; }
    }
}
