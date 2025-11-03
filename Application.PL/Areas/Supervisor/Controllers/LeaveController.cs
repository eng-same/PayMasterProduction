using Application.DAL.Models;
using Application.PL.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Application.PL.Areas.Supervisor.Controllers
{
    [Area("Supervisor")]
    [Authorize]
    public class LeaveController : Controller
    {
        private readonly ReportRepository _repo;
        private readonly UserManager<User> _userManager;

        public LeaveController(ReportRepository repo, UserManager<User> userManager)
        {
            _repo = repo;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            var uid = _userManager.GetUserId(User);
            var company = await _repo.GetCompanyForSupervisorAsync(uid);
            if (company == null) return Forbid();

            var list = await _repo.GetLeaveRequestsForCompanyAsync(company.Id);
            return View("Index", list);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(int id)
        {
            var uid = _userManager.GetUserId(User);
            var company = await _repo.GetCompanyForSupervisorAsync(uid);
            if (company == null) return Forbid();

            var (ok, err) = await _repo.ApproveLeaveRequestAsync(company.Id, id, uid);
            if (!ok) return BadRequest(new { message = err });
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reject(int id)
        {
            var uid = _userManager.GetUserId(User);
            var company = await _repo.GetCompanyForSupervisorAsync(uid);
            if (company == null) return Forbid();

            var (ok, err) = await _repo.RejectLeaveRequestAsync(company.Id, id, uid);
            if (!ok) return BadRequest(new { message = err });
            return RedirectToAction(nameof(Index));
        }
    }
}
