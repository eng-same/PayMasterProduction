namespace Application.PL.ViewModels
{
    public class SelectEmployeeItem
    {
        public int EmployeeId { get; set; }
        public string Label { get; set; } = null!;
        public bool Selected { get; set; }
    }
}
