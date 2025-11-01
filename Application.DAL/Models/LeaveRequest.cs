namespace Application.DAL.Models
{
    public class LeaveRequest
    {
        public int Id { get; set; }

        public string LeaveType { get; set; }     // "Sick", "Unpaid"

        public DateTime StartDate { get; set; }
        
        public DateTime EndDate { get; set; }
        
        public string Reason { get; set; }
        
        public string Status { get; set; } // Pending, Approved, Rejected

        public DateTime SubmittedAt { get; set; }
        
        //FK
        public int? EmployeeId { get; set; }

        // Navigation properties

        public Employee Employee { get; set; }
    }
}
