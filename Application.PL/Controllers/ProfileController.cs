using Application.BLL.Servicies;
using Application.DAL.Models;
using Application.PL.Services;
using Application.PL.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Application.PL.Controllers
{
    [Authorize]
    public class ProfileController : Controller
    {
        private readonly UserManager<User> _userManager;
        private readonly IFileService _fileService;
        private readonly EmployeeService _employeeService;

        // allowed image extensions (same convention as earlier)
        private readonly string[] _allowedImageExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif" };

        public ProfileController(UserManager<User> userManager,
                                 IFileService fileService,
                                 EmployeeService employeeService)
        {
            _userManager = userManager;
            _fileService = fileService;
            _employeeService = employeeService;
        }

        // GET: /Profile
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var user = await GetCurrentUserAsync();
            if (user == null) return Challenge();

            var employee = await _employeeService.GetByUserIdAsync(user.Id);

            var vm = new ProfileVM
            {
                Id = user.Id,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Email = user.Email,
                UserName = user.UserName,
                PhoneNumber = user.PhoneNumber,
                ProfilePic = user.ProfilePic,
                IsActive = user.IsActive,
                EmployeeId = employee?.Id,
                EmployeePosition = employee?.Position,
                EmployeeCompanyId = employee?.CompanyId
            };

            ViewData["Title"] = "My Profile";
            
            return View(vm);
        }

        // GET: /Profile/Edit
        [HttpGet]
        public async Task<IActionResult> Edit()
        {
            var user = await GetCurrentUserAsync();
            if (user == null) return Challenge();

            var vm = new ProfileEditVM
            {
                Id = user.Id,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Email = user.Email,
                UserName = user.UserName,
                PhoneNumber = user.PhoneNumber,
                ProfilePic = user.ProfilePic
            };

            ViewData["Title"] = "Edit Profile";
            
            return View(vm);
        }

        // POST: /Profile/Edit
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(ProfileEditVM model)
        {
            if (!ModelState.IsValid)
            {
                ViewData["Title"] = "Edit Profile";
                
                return View(model);
            }

            var user = await _userManager.FindByIdAsync(model.Id);
            if (user == null) return Challenge();

            // handle avatar upload if present
            if (model.ProfileImageFile != null && model.ProfileImageFile.Length > 0)
            {
                try
                {
                    var newPath = await _fileService.SaveFile(model.ProfileImageFile, _allowedImageExtensions);

                    // delete previous file if it was saved under /images
                    if (!string.IsNullOrEmpty(user.ProfilePic) && user.ProfilePic.StartsWith("/images", StringComparison.OrdinalIgnoreCase))
                    {
                        try
                        {
                            _fileService.DeleteFile(user.ProfilePic);
                        }
                        catch
                        {
                            // swallow — deletion failure shouldn't block user update
                        }
                    }

                    user.ProfilePic = newPath;
                }
                catch (InvalidOperationException invEx)
                {
                    ModelState.AddModelError(nameof(model.ProfileImageFile), invEx.Message);
                    ViewData["Title"] = "Edit Profile";
                    
                    return View(model);
                }
            }

            // update basic fields
            user.FirstName = model.FirstName?.Trim();
            user.LastName = model.LastName?.Trim();
            user.PhoneNumber = model.PhoneNumber?.Trim();
            // allow email change but ensure uniqueness if you like — here we assign directly:
            if (!string.Equals(user.Email, model.Email, StringComparison.OrdinalIgnoreCase))
            {
                var existing = await _userManager.FindByEmailAsync(model.Email);
                if (existing != null && existing.Id != user.Id)
                {
                    ModelState.AddModelError(nameof(model.Email), "Email is already taken.");
                    ViewData["Title"] = "Edit Profile";
                    
                    return View(model);
                }
                user.Email = model.Email?.Trim();
                user.UserName = model.Email?.Trim(); // keep username synced with email as in your Auth controller
            }

            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
            {
                foreach (var err in result.Errors) ModelState.AddModelError("", err.Description);
                ViewData["Title"] = "Edit Profile";
                
                return View(model);
            }

            TempData["ProfileSuccess"] = "Profile updated successfully.";
            return RedirectToAction(nameof(Index));
        }

        // helper
        private Task<User?> GetCurrentUserAsync()
        {
            return _userManager.GetUserAsync(User);
        }
    }
}
