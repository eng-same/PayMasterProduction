using Application.DAL.Models;
using Application.PL.Services;
using Application.PL.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Application.PL.Areas.Supervisor.Controllers
{
    [Area("Supervisor")]
    [Authorize] // additional supervisor/company-permission enforced below
    public class CompanyController : Controller
    {
        private readonly ReportRepository _reportRepo;
        private readonly UserManager<User> _userManager;

        public CompanyController(ReportRepository reportRepo, UserManager<User> userManager)
        {
            _reportRepo = reportRepo;
            _userManager = userManager;
        }

        // GET: /Company/Company/Dashboard
        public async Task<IActionResult> Dashboard()
        {
            var currentUserId = _userManager.GetUserId(User);
            var company = await _reportRepo.GetCompanyForSupervisorAsync(currentUserId);
            if (company == null) return Forbid();

            var vm = await _reportRepo.GetCompanyDashboardAsync(company.Id, months: 6);
            return View(vm);
        }

        // POST: pay unpaid invoice
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PayInvoice(int id)
        {
            var currentUserId = _userManager.GetUserId(User);
            var company = await _reportRepo.GetCompanyForSupervisorAsync(currentUserId);
            if (company == null) return Forbid();

            var success = await _reportRepo.PayInvoiceForCompanyAsync(company.Id, id);
            if (!success) return BadRequest(new { message = "Invoice not found or already paid." });
            return Json(new { id, ok = true });
        }

        // POST: add existing Identity user (by email) as employee
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddExistingUserAsEmployee(string userEmail, string position)
        {
            var currentUserId = _userManager.GetUserId(User);
            var company = await _reportRepo.GetCompanyForSupervisorAsync(currentUserId);
            if (company == null) return Forbid();

            var result = await _reportRepo.AddExistingUserAsEmployeeAsync(company.Id, userEmail, position);
            if (!result.Success) return BadRequest(new { message = result.Error });
            return Json(new { employeeId = result.EmployeeId, ok = true });
        }

        // POST: create new Identity user and add as employee
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateAndAddEmployee(CreateEmployeeRequest req)
        {
            var currentUserId = _userManager.GetUserId(User);
            var company = await _reportRepo.GetCompanyForSupervisorAsync(currentUserId);
            if (company == null) return Forbid();

            // validate basic inputs
            if (string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Password))
                return BadRequest(new { message = "Email and password required." });

            // create identity user
            var user = new User
            {
                UserName = req.Email,
                Email = req.Email,
                FirstName = req.FirstName,
                LastName = req.LastName,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };

            var createRes = await _userManager.CreateAsync(user, req.Password);
            if (!createRes.Succeeded)
            {
                var errors = string.Join("; ", createRes.Errors.Select(e => e.Description));
                return BadRequest(new { message = errors });
            }

            // optionally add a role or email confirmation here...

            // now add employee row via repository helper
            var addRes = await _reportRepo.AddExistingUserAsEmployeeAsync(company.Id, req.Email, req.Position);
            if (!addRes.Success)
            {
                // rollback user creation? depends on policy - here we return error (could delete user)
                return BadRequest(new { message = addRes.Error });
            }

            return Json(new { employeeId = addRes.EmployeeId, ok = true });
        }
    }

}
