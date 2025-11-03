namespace Application.PL.ViewModels
{
    public class ProfileVM
    {
        public string Id { get; set; } = null!;
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? FullName => $"{FirstName} {LastName}".Trim();
        public string? Email { get; set; }
        public string? UserName { get; set; }
        public string? PhoneNumber { get; set; }
        public string? ProfilePic { get; set; }
        public bool IsActive { get; set; }

        // optional employee info
        public int? EmployeeId { get; set; }
        public string? EmployeePosition { get; set; }
        public int? EmployeeCompanyId { get; set; }
    }
}
