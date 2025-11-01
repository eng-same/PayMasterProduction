namespace Application.PL.ViewModels
{
    public class QrResultVm
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public int? EmployeeId { get; set; }
        public int? CompanyId { get; set; }
        public int? AttendanceId { get; set; }
        public bool IsCheckout { get; set; }
    }
}
