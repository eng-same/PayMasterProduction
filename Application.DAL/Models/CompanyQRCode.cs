namespace Application.DAL.Models
{
    public class CompanyQRCode
    {
        public int Id { get; set; }
       
        public string QRCodeToken { get; set; }

        public DateTime GeneratedAt { get; set; }

        public DateTime ExpiryDate { get; set; }

        public bool IsActive { get; set; } = true;

        //FK
        public int CompanyId { get; set; }

        // Navigation property
        public Company Company { get; set; }
    }
}
