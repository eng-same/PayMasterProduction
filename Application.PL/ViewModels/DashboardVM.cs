namespace Application.PL.ViewModels
{
    public class DashboardVM
    {// totals
        public int TotalCompanies { get; set; }
        public int TotalEmployees { get; set; }
        public int TotalActiveEmployees { get; set; }
        public int TotalInvoices { get; set; }
        public decimal TotalOutstandingAmount { get; set; }


        // monthly chart
        public List<MonthlyPoint> MonthlyPoints { get; set; } = new();


        // companies with unpaid invoices
        public List<TopUnpaidCompanyVM> CompaniesWithUnpaidInvoices { get; set; } = new();


        // employees with most deductions
        public List<TopLateEmployeeVM> TopLateEmployees { get; set; } = new();


        // attendance quick stats
        public int TotalAttendances { get; set; }
        public decimal AvgAttendancePerCompany { get; set; }
    }


    public class MonthlyPoint
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public decimal Invoiced { get; set; }
        public decimal Paid { get; set; }


        // convenience label for chart
        public string Label => new DateTime(Year, Month, 1).ToString("MMM yyyy");
    }


    public class TopUnpaidCompanyVM
    {
        public int CompanyId { get; set; }
        public string CompanyName { get; set; }
        public decimal UnpaidAmount { get; set; }
        public int UnpaidInvoiceCount { get; set; }
    }


    public class TopLateEmployeeVM
    {
        public int EmployeeId { get; set; }
        public string FullName { get; set; }
        public int CompanyId { get; set; }
        public int TotalMinutesDeducted { get; set; }
        public decimal TotalAmountDeducted { get; set; }
    }
}
