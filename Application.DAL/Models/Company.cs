namespace Application.DAL.Models
{
    public class Company
    {
        public int Id { get; set; }
        
        public string Name { get; set; }
        
        public string Timezone { get; set; }
        
        public int DefaultGraceMinutes { get; set; }

        public string? Address { get; set; }
        
        public string PhoneNumber { get; set; }
        
        public string Email { get; set; }
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public bool IsActive { get; set; } =true;

        public decimal? BillingRatePerEmployee { get; set; }


        // Navigation properties
        public virtual ICollection<Employee> Employees { get; set; } = new List<Employee>();
        public ICollection<CompanySupervisor> Supervisors = new List<CompanySupervisor>();
        public virtual ICollection<Job> Jobs { get; set; } = new List<Job>();
        public virtual ICollection<Device> Devices { get; set; } = new List<Device>();
        public virtual ICollection<CompanyQRCode> CompanyQRCodes { get; set; } = new List<CompanyQRCode>();
        public virtual ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();


    }
}
