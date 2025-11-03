using Microsoft.AspNetCore.Mvc.Rendering;

namespace Application.PL.ViewModels
{
    public class AddExistingUserVM
    {

        // identity data
        public string UserId { get; set; }
        public string Email { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }

        public bool NotFound { get; set; } = false;
        public bool HasRole { get; set; } = false; // true if identity user has role "Employee"
        public bool IsAlreadyEmployee { get; set; } = false;

        // company context
        public int CompanyId { get; set; }

        // jobs dropdown
        public List<SelectListItem> Jobs { get; set; } = new List<SelectListItem>();
    }
}
