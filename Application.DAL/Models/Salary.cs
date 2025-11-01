namespace Application.DAL.Models
{
    public class Salary
    {
        //need to get reviewed if there is a need for more fields
        public int Id { get; set; }
        
        public decimal BaseAmount { get; set; }
        
        public DateTime PayDate { get; set; }
        
        //FK
        public int EmployeeId { get; set; }
        // Navigation property
        public Employee Employee { get; set; }
    }
}
