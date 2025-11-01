namespace Application.DAL.Models
{
    public class Device
    {
        public int Id { get; set; }
        public string Name { get; set; } // e.g. "Reception Tablet"
        public string SerialNumber { get; set; }
        public string DeviceType { get; set; }    // "MobileApp","Kiosk","Biometric"
        //public DateTime? LastSeenAt { get; set; }

        //FK
        public int CompanyId { get; set; }

        // Navigation property
        public Company Company { get; set; }
    }
}
