namespace Application.PL.ViewModels
{
    public class CompanyDetailsVM
    {
        // Basic company snapshot
        public int CompanyId { get; set; }
        public string Name { get; set; }
        public string Timezone { get; set; }
        public int DefaultGraceMinutes { get; set; }
        public string? Address { get; set; }
        public string PhoneNumber { get; set; }
        public string Email { get; set; }
        public bool IsActive { get; set; }
        public decimal? BillingRatePerEmployee { get; set; }

        // Derived / related info
        public int ActiveEmployeeCount { get; set; }

        // Latest QR info (if any)
        public int? LatestQrId { get; set; }
        public string? LatestQrToken { get; set; }
        public DateTime? LatestQrGeneratedAt { get; set; }
        public DateTime? LatestQrExpiryDate { get; set; }

        // Absolute URL to the PNG endpoint for the latest QR (null if none)
        // e.g. https://yourhost/api/qr/{id}
        public string? LatestQrImageUrl { get; set; }

        // Recent invoices (most recent first)
        public List<InvoiceListItemVM> RecentInvoices { get; set; } = new List<InvoiceListItemVM>();

        // Supervisors
        public List<SupervisorVM> Supervisors { get; set; } = new List<SupervisorVM>();
    }

    public class InvoiceListItemVM
    {
        public int Id { get; set; }
        public DateTime Date { get; set; }
        public int ActiveEmployeeCount { get; set; }
        public decimal RatePerEmployee { get; set; }
        public decimal TotalAmount { get; set; }
        public bool IsPaid { get; set; }
    }

    public class SupervisorVM
    {
        public int Id { get; set; }               // CompanySupervisor id (optional)
        public string UserId { get; set; }       // Identity user id
        public string FullName { get; set; }
        public string Email { get; set; }
    }
}
