namespace Application.PL.ViewModels
{
    public class CompanyDashboardVM
    {
        public int CompanyId { get; set; }
        public string CompanyName { get; set; }

        public int TotalEmployees { get; set; }
        public decimal TotalUnpaidAmount { get; set; }

        public List<RecentUnpaidInvoiceVM> RecentUnpaidInvoices { get; set; } = new();
        public List<MonthlyPoint> MonthlyAttendance { get; set; } = new();

        public List<EmployeeReportVM> EmployeeReports { get; set; } = new();
        public List<EmployeeReportVM> AbsentEmployees { get; set; } = new();
    }

    public class RecentUnpaidInvoiceVM
    {
        public int InvoiceId { get; set; }
        public DateTime Date { get; set; }
        public decimal Amount { get; set; }
        public string Notes { get; set; }
    }

    public class EmployeeReportVM
    {
        public int EmployeeId { get; set; }
        public string FullName { get; set; }
        public bool IsActive { get; set; }
        public decimal TotalSalaries { get; set; }
        public decimal TotalOvertimes { get; set; }
        public decimal TotalDeductions { get; set; }
        public int AttendanceCountThisMonth { get; set; }
    }

 }
