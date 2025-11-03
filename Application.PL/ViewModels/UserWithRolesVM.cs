namespace Application.PL.ViewModels
{
    public class UserWithRolesVM
    {
        public string Id { get; set; } = null!;
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string FullName { get; set; } = null!;
        public string? Email { get; set; }
        public string? UserName { get; set; }
        public string? ProfilePic { get; set; }
        public bool IsActive { get; set; }
        public List<string> Roles { get; set; } = new();
        public int? EmployeeId { get; set; }
        public string? EmployeeName { get; set; }
        public bool IsCompanySupervisor { get; set; }
    }
}
