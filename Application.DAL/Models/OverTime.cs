namespace Application.DAL.Models
{
    public class OverTime
    {
        public int Id { get; set; }
        public DateTime Date { get; set; }
        public decimal Hours { get; set; }
        public decimal Rate { get; set; }
        //FK
        public int EmployeeId { get; set; }
        // Navigation property
        public Employee Employee { get; set; }
    }
}
