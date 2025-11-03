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

        // If true, this attendance segment is excused (e.g. approved leave) and should be excluded from missed-minutes calculations
        public bool IsExcused { get; set; } = false;
    }
}
