using System.Configuration;

namespace Application.DAL.Models
{
    public class Employee
    {
        public int Id { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Email { get; set; }
        public string Position { get; set; }
        public int? EmployeeNo { get; set; }
        public DateTime HireDate { get; set; }
        public bool IsActive { get; set; }

        // FK -> Identity user (optional)
        public string? UserId { get; set; }

        // Company required
        public int CompanyId { get; set; }

        // Job optional (allow SetNull on delete)
        public int? JobId { get; set; }

        public User User { get; set; }
        public Company Company { get; set; }
        public Job Job { get; set; }

        public ICollection<Attendance> Attendances { get; set; } = new List<Attendance>();
        public ICollection<PayrollDeduction> PayrollDeductions { get; set; } = new List<PayrollDeduction>();
        public ICollection<LeaveRequest> LeaveRequests { get; set; } = new List<LeaveRequest>();
        public ICollection<Salary> Salaries { get; set; } = new List<Salary>();
        public ICollection<OverTime> OverTimes { get; set; } = new List<OverTime>();
    }
}