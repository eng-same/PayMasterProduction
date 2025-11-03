using Microsoft.AspNetCore.Mvc.Rendering;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Application.PL.Areas.CompanyDesk.Models
{
    public class LeaveRequestCreateVm
    {
        public int CompanyId { get; set; }

        [Required]
        [Display(Name = "Employee")]
        public int EmployeeId { get; set; }

        [Required]
        [Display(Name = "Leave Type")]
        public string LeaveType { get; set; }

        [Required]
        [Display(Name = "Reason")]
        public string Reason { get; set; }

        // For supervisor UI: list of employees to choose from
        public IEnumerable<SelectListItem> Employees { get; set; }
    }
}
