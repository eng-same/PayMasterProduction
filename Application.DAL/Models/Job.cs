namespace Application.DAL.Models
{
    public class Job
    {
        public int Id { get; set; }
        
        public string Title { get; set; }
        
        public string Description { get; set; }
        
        public decimal Salary { get; set; }

        public string Status { get; set; }

        public int StandardDurationMinutes { get; set; }

        public TimeSpan? StandardStartTime { get; set; }//neednto change shiftpattern

        public TimeSpan? StandardEndTime { get; set; }

        public int AllowedDailyMinutes { get; set; } //max allowed working minutes before deduction
        
        //Fk
        public int CompanyId { get; set; }

        // Navigation properties

        public Company Company { get; set; }


    }
}
