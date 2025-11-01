using Application.DAL.Models;
using Application.PL.Services;
using Application.BLL.Servicies;
using Application.PL.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Application.PL.Controllers
{

    public class AuthController : Controller
    {
        private readonly SignInManager<User> _signInManager;
        private readonly UserManager<User> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly IFileService _fileService;
        private readonly EmployeeService _employeeService;

        public AuthController(SignInManager<User> signInManager,
                              UserManager<User> userManager,
                              RoleManager<IdentityRole> roleManager,
                              IFileService fileService,
                              EmployeeService employeeService)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _roleManager = roleManager;
            _fileService = fileService;
            _employeeService = employeeService;
        }

        [HttpGet]
        public IActionResult Register()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Register(UserRegesterVM model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var userExists = await _userManager.FindByEmailAsync(model.Email);
            if (userExists != null)
            {
                ModelState.AddModelError("", "User already exists");
                return View(model);
            }

            var newUser = new User
            {
                Email = model.Email,
                UserName = model.Email,
                FirstName = model.FirstName,
                LastName = model.LastName
            };

            if (model.ProfilePic != null)
            {
                var allowedExtensions = new string[] { ".jpg", ".jpeg", ".png" };
                try
                {
                    var profilePicName = await _fileService.SaveFile(model.ProfilePic, allowedExtensions);
                    newUser.ProfilePic = profilePicName;
                }
                catch
                {
                    ModelState.AddModelError("", "Failed to save profile picture");
                    return View(model);
                }
            }

            var result = await _userManager.CreateAsync(newUser, model.Password);
            if (result.Succeeded)
            {
                // assign Employee role by default
                const string employeeRole = "Employee";

                if (!await _roleManager.RoleExistsAsync(employeeRole))
                    await _roleManager.CreateAsync(new IdentityRole(employeeRole));

                await _userManager.AddToRoleAsync(newUser, employeeRole);

                await _signInManager.SignInAsync(newUser, isPersistent: model.RememberMe);

                // redirect based on role
                return RedirectToRoleHome(employeeRole);
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError("", error.Description);
            }

            return View(model);
        }

        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Login(LoginVM model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user == null)
            {
                ModelState.AddModelError("", "Invalid login attempt");
                return View(model);
            }

            var result = await _signInManager.PasswordSignInAsync(
                user.UserName,
                model.Password,
                isPersistent: model.RememberMe,
                lockoutOnFailure: false);

            if (result.Succeeded)
            {
                var roles = await _userManager.GetRolesAsync(user);
                var role = roles.FirstOrDefault();

                // FIX: Retrieve Employee entity for the user to get CompanyId
                // This assumes you have a way to get Employee by user.Id, e.g. via a service or db context.
                //Example using a service (replace with your actual implementation):
                var employee = await _employeeService.GetByUserIdAsync(user.Id);
                if (employee != null)
                    await _userManager.AddClaimAsync(user, new Claim("companyId", employee.CompanyId.ToString()));

                // For now, comment out the problematic line to avoid CS0120
                // await _userManager.AddClaimAsync(user, new Claim("companyId", Employee.CompanyId.ToString()));

                return RedirectToRoleHome(role);
            }

            ModelState.AddModelError("", "Invalid login attempt.");
            return View(model);
        }
        [HttpPost]
        public async Task<IActionResult> Logout()
        {
            //it want to be chinged later to match defult area
            await _signInManager.SignOutAsync();
            return RedirectToAction("Index", "Visitor");
        }

        // Helper method for clean redirection by role
        private IActionResult RedirectToRoleHome(string role)
        {
            if (string.IsNullOrEmpty(role))
                return RedirectToAction("Index", "Home");

            role = role.ToLower();

            if (role == "admin")
                return RedirectToAction("Index", "Home", new { Area = "Admin" });

            if (role == "supervisor")
                return RedirectToAction("Dashboard", "Home", new { Area = "Supervisor" });

            if (role == "employee")
                return RedirectToAction("Index", "Home", new { Area = "Employee" });

            return RedirectToAction("Index", "Home");
        }
    }

}
