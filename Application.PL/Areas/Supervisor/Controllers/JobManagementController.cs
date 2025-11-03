using Application.DAL.Models;
using Application.PL.Services;
using Application.PL.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Application.PL.Areas.Supervisor.Controllers
{
    [Area("Supervisor")]
    [Authorize]
    public class JobManagementController : Controller
    {
        private readonly UserManager<User> _userManager;
        private readonly ReportRepository _reportRepo; // your repo that handles companies/employees/jobs

        public JobManagementController(UserManager<User> userManager, ReportRepository reportRepo)
        {
            _userManager = userManager;
            _reportRepo = reportRepo;
        }

        // --- Jobs list ---
        [HttpGet]
        public async Task<IActionResult> JobsIndex()
        {
            var currentUserId = _userManager.GetUserId(User);
            var company = await _reportRepo.GetCompanyForSupervisorAsync(currentUserId);
            if (company == null) return Forbid();

            var jobs = await _reportRepo.GetJobsForCompanyAsync(company.Id);
            // Use the controller's standard Index view (located at Areas/Supervisor/Views/JobManagement/Index.cshtml)
            return View("Index", jobs);
        }

        // GET: create job form
        [HttpGet]
        public async Task<IActionResult> CreateJob()
        {
            var currentUserId = _userManager.GetUserId(User);
            var company = await _reportRepo.GetCompanyForSupervisorAsync(currentUserId);
            if (company == null) return Forbid();

            var model = new Job { CompanyId = company.Id };
            return View("Create", model);
        }

        // POST: create job
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateJob(Job model)
        {
            var currentUserId = _userManager.GetUserId(User);
            var company = await _reportRepo.GetCompanyForSupervisorAsync(currentUserId);
            if (company == null) return Forbid();

            if (!ModelState.IsValid) return View("Create", model);

            model.CompanyId = company.Id;
            var (success, error, jobId) = await _reportRepo.CreateJobAsync(model);
            if (!success) ModelState.AddModelError("", error);

            if (!success) return View("Create", model);

            TempData["AlertSuccess"] = "Job created successfully.";
            return RedirectToAction(nameof(JobsIndex));
        }

        // GET: edit
        [HttpGet]
        public async Task<IActionResult> EditJob(int id)
        {
            var currentUserId = _userManager.GetUserId(User);
            var company = await _reportRepo.GetCompanyForSupervisorAsync(currentUserId);
            if (company == null) return Forbid();

            var job = await _reportRepo.GetJobByIdAsync(company.Id, id);
            if (job == null) return NotFound();

            return View("Edit", job);
        }

        // POST: edit
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditJob(Job model)
        {
            var currentUserId = _userManager.GetUserId(User);
            var company = await _reportRepo.GetCompanyForSupervisorAsync(currentUserId);
            if (company == null) return Forbid();

            if (!ModelState.IsValid) return View("Edit", model);

            var (success, error) = await _reportRepo.UpdateJobAsync(company.Id, model);
            if (!success)
            {
                ModelState.AddModelError("", error);
                return View("Edit", model);
            }

            TempData["AlertSuccess"] = "Job updated.";
            return RedirectToAction(nameof(JobsIndex));
        }

        // POST: delete
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteJob(int id)
        {
            var currentUserId = _userManager.GetUserId(User);
            var company = await _reportRepo.GetCompanyForSupervisorAsync(currentUserId);
            if (company == null) return Forbid();

            var (success, error) = await _reportRepo.DeleteJobAsync(company.Id, id);
            if (!success) return BadRequest(new { message = error });

            TempData["AlertSuccess"] = "Job deleted.";
            return RedirectToAction(nameof(JobsIndex));
        }

        [HttpGet]
        public async Task<IActionResult> CreateAndAddEmployee()
        {
            var currentUserId = _userManager.GetUserId(User);
            var company = await _reportRepo.GetCompanyForSupervisorAsync(currentUserId);
            if (company == null) return Forbid();

            var jobs = await _reportRepo.GetJobsForCompanyAsync(company.Id);

            // we'll pass a small viewmodel with Jobs and default CreateEmployeeRequest
            var vm = new CreateEmployeeVM
            {
                CompanyId = company.Id,
                Jobs = jobs.Select(j => new SelectListItem { Value = j.Id.ToString(), Text = j.Title }).ToList(),
                Request = new CreateEmployeeRequest()
            };

            return View("CreateAndAddEmployee", vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateAndAddEmployee(CreateEmployeeRequest req)
        {
            var currentUserId = _userManager.GetUserId(User);
            var company = await _reportRepo.GetCompanyForSupervisorAsync(currentUserId);
            if (company == null) return Forbid();

            if (string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Password))
                return BadRequest(new { message = "Email and password required." });

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

            // add employee (validate job ownership inside repository)
            var addRes = await _reportRepo.AddExistingUserAsEmployeeAsync(company.Id, user.Id, req.Position, req.JobId);
            if (!addRes.Success)
            {
                // Compensating delete: remove the Identity user we just created
                await _userManager.DeleteAsync(user);
                return BadRequest(new { message = addRes.Error });
            }

            return Json(new { employeeId = addRes.EmployeeId, ok = true });
        }


        // Add near other actions in Supervisor/JobManagementController
        [HttpGet]
        public async Task<IActionResult> AddExistingUser(string email)
        {
            if (string.IsNullOrWhiteSpace(email)) return BadRequest("Email required.");

            // find identity user by email
            var identityUser = await _userManager.FindByEmailAsync(email);
            if (identityUser == null)
            {
                // show a minimal page saying user not found
                return View("AddExistingUser", new AddExistingUserVM { NotFound = true, Email = email });
            }

            // check role membership - must have Identity role "Employee"
            var inRole = await _userManager.IsInRoleAsync(identityUser, "Employee");
            if (!inRole)
            {
                return View("AddExistingUser", new AddExistingUserVM
                {
                    Email = email,
                    HasRole = false,
                    UserId = identityUser.Id,
                    FirstName = identityUser.FirstName,
                    LastName = identityUser.LastName
                });
            }

            // ensure current supervisor's company context
            var currentUserId = _userManager.GetUserId(User);
            var company = await _reportRepo.GetCompanyForSupervisorAsync(currentUserId);
            if (company == null) return Forbid();

            // check if user already has Employee row in this company
            var alreadyEmployee = await _reportRepo.IsUserAlreadyEmployeeInCompanyAsync(identityUser.Id, company.Id);

            // jobs for combobox
            var jobs = await _reportRepo.GetJobsForCompanyAsync(company.Id);

            var vm = new AddExistingUserVM
            {
                Email = identityUser.Email,
                UserId = identityUser.Id,
                FirstName = identityUser.FirstName,
                LastName = identityUser.LastName,
                HasRole = true,
                CompanyId = company.Id,
                IsAlreadyEmployee = alreadyEmployee,
                Jobs = jobs.Select(j => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem { Value = j.Id.ToString(), Text = j.Title }).ToList()
            };

            return View("AddExistingUser", vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddExistingUserAsEmployee(string userId, string position, int? jobId, int companyId)
        {
            // Only supervisors of the company may do this
            var currentUserId = _userManager.GetUserId(User);
            var company = await _reportRepo.GetCompanyForSupervisorAsync(currentUserId);
            if (company == null || company.Id != companyId) return Forbid();

            // Find identity user
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return BadRequest(new { message = "User not found." });

            // Ensure the identity user has role "Employee"
            var inRole = await _userManager.IsInRoleAsync(user, "Employee");
            if (!inRole) return BadRequest(new { message = "Identity user does not have the required 'Employee' role." });

            // Verify user isn't already an Employee row in this company
            var already = await _reportRepo.IsUserAlreadyEmployeeInCompanyAsync(user.Id, companyId);
            if (already) return BadRequest(new { message = "User is already an employee of this company." });

            // Validate job belongs to company
            if (jobId.HasValue)
            {
                var job = await _reportRepo.GetJobByIdAsync(companyId, jobId.Value);
                if (job == null) return BadRequest(new { message = "Selected job does not belong to this company." });
            }

            // Use repository overload that accepts userId
            var addRes = await _reportRepo.AddExistingUserAsEmployeeAsync(companyId, user.Id, position, jobId);
            if (!addRes.Success) return BadRequest(new { message = addRes.Error });

            // prefer redirect back to jobs index or details
            return RedirectToAction(nameof(JobsIndex));
        }

    }
}
