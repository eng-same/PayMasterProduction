namespace Application.DAL.Models
{
    public class Invoice
    {
        public int Id { get; set; }

        // FK -> Company
        public int CompanyId { get; set; }
        public Company Company { get; set; }
        // Snapshot fields
        public int ActiveEmployeeCount { get; set; }        // number of active employees at billing calculation time
        public decimal RatePerEmployee { get; set; }       // rate applied when billing was created
        public decimal TotalAmount { get; set; }           // ActiveEmployeeCount * RatePerEmployee

        // Status / audit fields
        public DateTime Date { get; set; } = DateTime.UtcNow;
        public bool IsPaid { get; set; } = false;
        public DateTime? PaidAt { get; set; }
        public string? Notes { get; set; }
    }
}
