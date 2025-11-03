using Application.DAL.Data;
using Application.DAL.Models;
using Application.PL.Services;
using Application.PL.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Application.PL.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize]
    public class UsersController : Controller
    {
        private readonly UserManager<User> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly AppDbContext _db;
        private readonly IFileService _fileService;

        // Allowed extensions used by FileService
        private readonly string[] _allowedImageExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif" };


        public UsersController(
            UserManager<User> userManager,
            RoleManager<IdentityRole> roleManager,
            AppDbContext db,
            IFileService fileService)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _db = db;
            _fileService = fileService;
        }

        // GET: /Admin/Users
        public async Task<IActionResult> Index()
        {
            var users = await _userManager.Users
                .OrderBy(u => u.LastName)
                .ToListAsync();

            // load roles in batches to avoid per-user DB round-trips
            var vmList = new List<UserWithRolesVM>();
            foreach (var user in users)
            {
                var roles = await _userManager.GetRolesAsync(user);
                // try find linked employee and supervisor quickly
                var employee = await _db.Employees
                    .AsNoTracking()
                    .FirstOrDefaultAsync(e => e.UserId == user.Id);

                var supervisor = await _db.CompanySupervisors
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.UserId == user.Id);

                vmList.Add(new UserWithRolesVM
                {
                    Id = user.Id,
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    FullName = $"{user.FirstName} {user.LastName}".Trim(),
                    Email = user.Email,
                    UserName = user.UserName,
                    ProfilePic = user.ProfilePic,
                    IsActive = user.IsActive,
                    Roles = roles.ToList(),
                    EmployeeId = employee?.Id,
                    EmployeeName = employee != null ? $"{employee.FirstName} {employee.LastName}" : null,
                    IsCompanySupervisor = supervisor != null
                });
            }

            return View(vmList);
        }

        // GET: /Admin/Users/Details/5
        public async Task<IActionResult> Details(string id)
        {
            if (string.IsNullOrEmpty(id)) return BadRequest();

            var user = await _userManager.Users
                .FirstOrDefaultAsync(u => u.Id == id);

            if (user == null) return NotFound();

            var roles = await _userManager.GetRolesAsync(user);
            var employee = await _db.Employees
                .Include(e => e.Company)
                .Include(e => e.Job)
                .FirstOrDefaultAsync(e => e.UserId == id);

            var supervisor = await _db.CompanySupervisors
                .Include(s => s.Company)
                .FirstOrDefaultAsync(s => s.UserId == id);

            var vm = new UserDetailsVM
            {
                User = user,
                Roles = roles.ToList(),
                Employee = employee,
                CompanySupervisor = supervisor
            };

            return View(vm);
        }

        // GET: /Admin/Users/Edit/5
        public async Task<IActionResult> Edit(string id)
        {
            if (string.IsNullOrEmpty(id)) return BadRequest();

            var user = await _userManager.Users.FirstOrDefaultAsync(u => u.Id == id);
            if (user == null) return NotFound();

            var allRoles = await _roleManager.Roles.Select(r => r.Name).ToListAsync();
            var userRoles = await _userManager.GetRolesAsync(user);

            var employees = await _db.Employees
                .AsNoTracking()
                .OrderBy(e => e.FirstName).ThenBy(e => e.LastName)
                .ToListAsync();

            var vm = new UserEditVM
            {
                Id = user.Id,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Email = user.Email,
                UserName = user.UserName,
                IsActive = user.IsActive,
                ProfilePic = user.ProfilePic,
                AvailableRoles = allRoles,
                AssignedRoles = userRoles.ToList(),
                EmployeeOptions = employees.Select(e => new SelectEmployeeItem
                {
                    EmployeeId = e.Id,
                    Label = $"{e.FirstName} {e.LastName} ({e.Email})",
                    Selected = e.UserId == user.Id
                }).ToList(),
                SelectedEmployeeId = employees.FirstOrDefault(e => e.UserId == user.Id)?.Id
            };

            return View(vm);
        }
        // POST: /Admin/Users/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, UserEditVM model)
        {
            if (id != model.Id) return BadRequest();

            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            if (!ModelState.IsValid)
            {
                // reload dropdowns
                model.AvailableRoles = await _roleManager.Roles.Select(r => r.Name).ToListAsync();
                var employees = await _db.Employees.AsNoTracking().ToListAsync();
                model.EmployeeOptions = employees.Select(e => new SelectEmployeeItem
                {
                    EmployeeId = e.Id,
                    Label = $"{e.FirstName} {e.LastName} ({e.Email})",
                    Selected = e.UserId == user.Id
                }).ToList();
                return View(model);
            }

            // Handle file upload if a file was provided
            if (model.ProfileImageFile != null && model.ProfileImageFile.Length > 0)
            {
                try
                {
                    // Save new file via FileService
                    var relativePath = await _fileService.SaveFile(model.ProfileImageFile, _allowedImageExtensions);

                    // Delete old file if it appears to be within images/products
                    if (!string.IsNullOrEmpty(user.ProfilePic) && user.ProfilePic.StartsWith("/images", StringComparison.OrdinalIgnoreCase))
                    {
                        try
                        {
                            _fileService.DeleteFile(user.ProfilePic);
                        }
                        catch (Exception ex)
                        {
                            // Log if you have logging (not to leak to user). We'll ignore here to be fault tolerant.
                            // e.g. _logger.LogWarning(ex, "Failed deleting old profile pic {Pic}", user.ProfilePic);
                        }
                    }

                    // Set user's profile pic to new path
                    user.ProfilePic = relativePath;
                }
                catch (InvalidOperationException invEx)
                {
                    ModelState.AddModelError(nameof(model.ProfileImageFile), invEx.Message);
                    model.AvailableRoles = await _roleManager.Roles.Select(r => r.Name).ToListAsync();
                    var employees = await _db.Employees.AsNoTracking().ToListAsync();
                    model.EmployeeOptions = employees.Select(e => new SelectEmployeeItem
                    {
                        EmployeeId = e.Id,
                        Label = $"{e.FirstName} {e.LastName} ({e.Email})",
                        Selected = e.UserId == user.Id
                    }).ToList();
                    return View(model);
                }
            }

            // update basic properties
            user.FirstName = model.FirstName?.Trim();
            user.LastName = model.LastName?.Trim();
            user.Email = model.Email?.Trim();
            user.UserName = model.UserName?.Trim();
            user.IsActive = model.IsActive;

            var updateResult = await _userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
            {
                foreach (var err in updateResult.Errors) ModelState.AddModelError("", err.Description);
                model.AvailableRoles = await _roleManager.Roles.Select(r => r.Name).ToListAsync();
                var employees = await _db.Employees.AsNoTracking().ToListAsync();
                model.EmployeeOptions = employees.Select(e => new SelectEmployeeItem
                {
                    EmployeeId = e.Id,
                    Label = $"{e.FirstName} {e.LastName} ({e.Email})",
                    Selected = e.UserId == user.Id
                }).ToList();
                return View(model);
            }

            // manage roles
            var currentRoles = (await _userManager.GetRolesAsync(user)).ToList();
            var rolesToAdd = model.AssignedRoles.Except(currentRoles).ToList();
            var rolesToRemove = currentRoles.Except(model.AssignedRoles).ToList();

            if (rolesToAdd.Any())
            {
                var addResult = await _userManager.AddToRolesAsync(user, rolesToAdd);
                if (!addResult.Succeeded) ModelState.AddModelError("", "Failed to add roles: " + string.Join(", ", addResult.Errors.Select(e => e.Description)));
            }

            if (rolesToRemove.Any())
            {
                var remResult = await _userManager.RemoveFromRolesAsync(user, rolesToRemove);
                if (!remResult.Succeeded) ModelState.AddModelError("", "Failed to remove roles: " + string.Join(", ", remResult.Errors.Select(e => e.Description)));
            }

            // employee linking/unlinking (same logic as prior implementation)
            var selectedEmployeeId = model.SelectedEmployeeId;
            if (selectedEmployeeId.HasValue)
            {
                var employee = await _db.Employees.FirstOrDefaultAsync(e => e.Id == selectedEmployeeId.Value);
                if (employee != null)
                {
                    // unlink previous employee for this user (if any and different)
                    var prevEmp = await _db.Employees.FirstOrDefaultAsync(e => e.UserId == user.Id && e.Id != employee.Id);
                    if (prevEmp != null) prevEmp.UserId = null;

                    // reassign this employee to user
                    employee.UserId = user.Id;
                }
            }
            else
            {
                // unlink any employee currently assigned to this user
                var prevEmp = await _db.Employees.FirstOrDefaultAsync(e => e.UserId == user.Id);
                if (prevEmp != null) prevEmp.UserId = null;
            }

            await _db.SaveChangesAsync();

            TempData["Success"] = "User updated.";
            return RedirectToAction(nameof(Index));
        }

        // POST: /Admin/Users/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(string id)
        {
            if (string.IsNullOrEmpty(id)) return BadRequest();

            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            // unlink employees that reference this user
            var employee = await _db.Employees.FirstOrDefaultAsync(e => e.UserId == id);
            if (employee != null)
            {
                employee.UserId = null;
            }

            // remove companysupervisor records referencing this user (or keep them -- depends on business logic)
            var supervisor = await _db.CompanySupervisors.FirstOrDefaultAsync(s => s.UserId == id);
            if (supervisor != null)
            {
                _db.CompanySupervisors.Remove(supervisor);
            }

            await _db.SaveChangesAsync();

            var delResult = await _userManager.DeleteAsync(user);
            if (!delResult.Succeeded)
            {
                TempData["Error"] = "Failed to delete user: " + string.Join(", ", delResult.Errors.Select(e => e.Description));
                return RedirectToAction(nameof(Index));
            }

            TempData["Success"] = "User deleted.";
            return RedirectToAction(nameof(Index));
        }
    }
}
