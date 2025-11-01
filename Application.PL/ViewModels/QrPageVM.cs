namespace Application.PL.ViewModels
{
    public class QrPageVM
    {

        public int CompanyId { get; set; }
        public int QrId { get; set; }
        public string Mode { get; set; } // "in" or "out"
    }
}
