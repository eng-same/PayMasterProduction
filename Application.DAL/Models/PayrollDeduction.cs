namespace Application.DAL.Models
{
    public class PayrollDeduction
    {
        public int Id { get; set; }

        public string Reason { get; set; }

        public int MinutesDeducted { get; set; }

        public decimal Amount { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        //FK
        public int EmployeeId { get; set; }

        // Navigation property
        public Employee Employee { get; set; }
    }
}
