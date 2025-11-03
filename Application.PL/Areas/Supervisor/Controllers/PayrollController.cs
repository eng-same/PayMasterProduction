using Application.DAL.Models;
using Application.PL.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Application.PL.Areas.Supervisor.Controllers
{
    [Area("Supervisor")]
    [Authorize]
    public class PayrollController : Controller
    {
        private readonly ReportRepository _repo;
        private readonly UserManager<User> _userManager;

        public PayrollController(ReportRepository repo, UserManager<User> userManager)
        {
            _repo = repo;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            var uid = _userManager.GetUserId(User);
            var company = await _repo.GetCompanyForSupervisorAsync(uid);
            if (company == null) return Forbid();

            // minimal view; actual payroll logic intentionally omitted
            return View("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RunPayroll()
        {
            var uid = _userManager.GetUserId(User);
            var company = await _repo.GetCompanyForSupervisorAsync(uid);
            if (company == null) return Forbid();

            var (ok, err) = await _repo.RunPayrollForCompanyAsync(company.Id);
            if (!ok) return BadRequest(new { message = err });
            TempData["Alert"] = "Payroll run queued (stub).";
            return RedirectToAction(nameof(Index));
        }
    }
}
