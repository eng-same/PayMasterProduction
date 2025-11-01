namespace Application.DAL.Models
{
    // Paired attendance segments (TimeIn/TimeOut)
    public class Attendance
    {
        public int Id { get; set; }
        
        public DateTime CheckInTime { get; set; }
        
        public DateTime? CheckOutTime { get; set; }

        public string Source { get; set; }

        //FK
        public int EmployeeId { get; set; }
        // Navigation property
        public Employee Employee { get; set; }
    }
}
