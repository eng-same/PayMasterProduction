using System;

namespace Application.DAL.Models
{
    public class VisitorRequest
    {
        public int Id { get; set; }

        // Visitor-provided fields
        public string CompanyName { get; set; } = string.Empty;
        public string ContactName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;

        // Number of employees requested (for tier calculation)
        public int NumberOfEmployees { get; set; }

        // Storing plain password temporarily so admin can create user on approval.
        // In production consider secure alternatives (email confirmation flow instead).
        public string Password { get; set; } = string.Empty;

        // Admin workflow fields
        public string Status { get; set; } = "Pending"; // Pending, Approved, Rejected
        public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ReviewedAt { get; set; }
        public string? ReviewedByAdminId { get; set; }
    }
}