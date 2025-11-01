using Application.PL.Services;
using Microsoft.AspNetCore.Mvc;

namespace Application.PL.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class HomeController : Controller
    {
        private readonly ReportRepository _reportRepo;

        public HomeController(ReportRepository reportRepo)
        {
            _reportRepo = reportRepo;
        }

        public async Task<IActionResult> Index()
        {
            // fetch last 12 months, top 5 companies with unpaid invoices, and top 5 late employees
            var vm = await _reportRepo.GetDashboardAsync(months: 12, topCompanies: 5, topLateEmployees: 5);
            return View(vm);
        }

        // (Optional) endpoint to mark invoice as paid quickly
        [HttpPost]
        public async Task<IActionResult> MarkInvoicePaid(int id)
        {
            var success = await _reportRepo.MarkInvoicePaidAsync(id);
            if (!success) return NotFound();
            return Ok(new { id });
        }
    }
}
