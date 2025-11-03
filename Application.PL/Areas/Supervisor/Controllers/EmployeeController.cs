using Application.DAL.Models;
using EmployeeModel= Application.DAL.Models.Employee;
using Application.PL.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Application.PL.Areas.Supervisor.Controllers
{
    [Area("Supervisor")]
    [Authorize]
    public class EmployeeController : Controller
    {
        private readonly ReportRepository _repo;
        private readonly UserManager<User> _userManager;

        public EmployeeController(ReportRepository repo, UserManager<User> userManager)
        {
            _repo = repo;
            _userManager = userManager;
        }

        // List employees for supervisor's company
        public async Task<IActionResult> Index()
        {
            var uid = _userManager.GetUserId(User);
            var company = await _repo.GetCompanyForSupervisorAsync(uid);
            if (company == null) return Forbid();

            var employees = await _repo.GetEmployeesForCompanyAsync(company.Id);
            return View("Index", employees);
        }

        // GET create
        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var uid = _userManager.GetUserId(User);
            var company = await _repo.GetCompanyForSupervisorAsync(uid);
            if (company == null) return Forbid();

            var emp = new EmployeeModel { CompanyId = company.Id, HireDate = DateTime.UtcNow };
            return View("Create", emp);
        }

        // POST create (no heavy validation as requested)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(EmployeeModel model)
        {
            var uid = _userManager.GetUserId(User);
            var company = await _repo.GetCompanyForSupervisorAsync(uid);
            if (company == null) return Forbid();

            model.CompanyId = company.Id;
            var (ok, err, id) = await _repo.CreateEmployeeAsync(model);
            if (!ok)
            {
                ModelState.AddModelError("", err);
                return View("Create", model);
            }

            return RedirectToAction(nameof(Index));
        }

        // GET edit
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var uid = _userManager.GetUserId(User);
            var company = await _repo.GetCompanyForSupervisorAsync(uid);
            if (company == null) return Forbid();

            var emp = await _repo.GetEmployeeByIdAsync(company.Id, id);
            if (emp == null) return NotFound();
            return View("Edit", emp);
        }

        // POST edit
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(EmployeeModel model)
        {
            var uid = _userManager.GetUserId(User);
            var company = await _repo.GetCompanyForSupervisorAsync(uid);
            if (company == null) return Forbid();

            var (ok, err) = await _repo.UpdateEmployeeAsync(company.Id, model);
            if (!ok)
            {
                ModelState.AddModelError("", err);
                return View("Edit", model);
            }

            return RedirectToAction(nameof(Index));
        }

        // POST delete
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var uid = _userManager.GetUserId(User);
            var company = await _repo.GetCompanyForSupervisorAsync(uid);
            if (company == null) return Forbid();

            var (ok, err) = await _repo.DeleteEmployeeAsync(company.Id, id);
            if (!ok) return BadRequest(new { message = err });
            return RedirectToAction(nameof(Index));
        }

        // Assign job via AJAX
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AssignJob(int employeeId, int? jobId)
        {
            var uid = _userManager.GetUserId(User);
            var company = await _repo.GetCompanyForSupervisorAsync(uid);
            if (company == null) return Forbid();

            try
            {
                await _repo.AssignJobToEmployeeAsync(employeeId, jobId);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }

            return Json(new { ok = true });
        }
    }
}
