using Microsoft.AspNetCore.Mvc.Rendering;

namespace Application.PL.ViewModels
{
    public class CreateEmployeeVM
    {
        public int CompanyId { get; set; }
        public CreateEmployeeRequest Request { get; set; } = new CreateEmployeeRequest();
        public List<SelectListItem> Jobs { get; set; } = new List<SelectListItem>();
    }
}
