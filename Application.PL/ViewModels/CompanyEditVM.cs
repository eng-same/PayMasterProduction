using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

namespace Application.PL.ViewModels
{
    public class CompanyEditVM
    {
        public int? Id { get; set; } // null for create
        [Required, StringLength(200)]
        public string Name { get; set; }

        public string Timezone { get; set; }
        public int DefaultGraceMinutes { get; set; }
        public string Address { get; set; }
        public string PhoneNumber { get; set; }
        [EmailAddress]
        public string Email { get; set; }
        public bool IsActive { get; set; } = true;

        [Display(Name = "Billing rate per employee")]
        [Range(0, double.MaxValue)]
        public decimal? BillingRatePerEmployee { get; set; }

        // Supervisors
        public IList<SelectListItem> AvailableSupervisors { get; set; } = new List<SelectListItem>();
        public List<string> SelectedSupervisorIds { get; set; } = new List<string>();
    }
}
