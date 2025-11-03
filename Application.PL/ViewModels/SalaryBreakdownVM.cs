using System;
using System.Collections.Generic;

namespace Application.PL.ViewModels
{
    public class SalaryBreakdownVM
    {
        public int SalaryId { get; set; }
        public decimal BaseAmount { get; set; }
        public decimal TotalOvertime { get; set; }
        public decimal TotalDeductions { get; set; }
        public decimal NetPay { get; set; }
        public DateTime PeriodStart { get; set; }
        public DateTime PeriodEnd { get; set; }

        public List<RecentOvertimeVM> Overtimes { get; set; } = new List<RecentOvertimeVM>();
        public List<RecentDeductionVM> Deductions { get; set; } = new List<RecentDeductionVM>();
    }
}