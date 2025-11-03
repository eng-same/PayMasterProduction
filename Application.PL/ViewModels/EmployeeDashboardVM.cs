namespace Application.PL.ViewModels
{
    public class EmployeeDashboardVM
    {
        public int EmployeeId { get; set; }
        public string FullName { get; set; }
        public string Position { get; set; }
        public int CompanyId { get; set; }
        public string CompanyName { get; set; }
        public string CompanyAddress { get; set; }

        public decimal TotalSalaries { get; set; }
        public decimal TotalOvertimes { get; set; }
        public decimal TotalDeductions { get; set; }

        public List<RecentSalaryVM> RecentSalaries { get; set; } = new();
        public List<RecentDeductionVM> RecentDeductions { get; set; } = new();
        public List<RecentOvertimeVM> RecentOvertimes { get; set; } = new();

        // attendance points per month (reuse MonthlyPoint shape)
        public List<MonthlyPoint> MonthlyAttendance { get; set; } = new();

        // recent attendance days for calendar
        public List<AttendanceDayVM> RecentAttendanceDays { get; set; } = new();
    }

    public class RecentSalaryVM
    {
        public int Id { get; set; }
        public DateTime PayDate { get; set; }
        public decimal Amount { get; set; }
    }

    public class RecentDeductionVM
    {
        public int Id { get; set; }
        public DateTime CreatedAt { get; set; }
        public decimal Amount { get; set; }
        public string Reason { get; set; }
    }

    public class RecentOvertimeVM
    {
        public int Id { get; set; }
        public DateTime Date { get; set; }
        public decimal Hours { get; set; }
        public decimal Rate { get; set; }
        public decimal Total => Hours * Rate;
    }

    public class AttendanceDayVM
    {
        public DateTime Date { get; set; }
        public string Status { get; set; } // e.g. "present", "absent", "leave"
    }

}
