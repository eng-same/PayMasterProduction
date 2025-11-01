namespace Application.PL.ViewModels
{
    public class CompanyListItemVM
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public bool IsActive { get; set; }
        public int ActiveEmployeeCount { get; set; }
        public decimal? BillingRatePerEmployee { get; set; }
        public decimal CurrentMonthlyAmount => BillingRatePerEmployee.HasValue
            ? BillingRatePerEmployee.Value * ActiveEmployeeCount
            : 0m;
    }
}
