using System;
using System.Linq;
using System.Threading.Tasks;
using Application.DAL.Data;
using Application.DAL.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Application.PL.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    [Route("admin/visitor-requests")]
    public class VisitorRequestAdminController : Controller
    {
        private readonly AppDbContext _db;
        private readonly UserManager<User> _userManager;

        public VisitorRequestAdminController(AppDbContext db, UserManager<User> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        [HttpGet("")]
        public async Task<IActionResult> Index()
        {
            var list = await _db.VisitorRequests.AsNoTracking().OrderByDescending(r => r.SubmittedAt).ToListAsync();
            return View(list);
        }

        [HttpGet("details/{id:int}")]
        public async Task<IActionResult> Details(int id)
        {
            var item = await _db.VisitorRequests.FindAsync(id);
            if (item == null) return NotFound();
            return View(item);
        }

        [HttpPost("approve/{id:int}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(int id)
        {
            var item = await _db.VisitorRequests.FindAsync(id);
            if (item == null) return NotFound();

            // Create Company
            var company = new Company
            {
                Name = item.CompanyName,
                Email = item.Email,
                PhoneNumber = item.Phone,
                Address = item.Message,
                IsActive = true,
                BillingRatePerEmployee = CalculateRate(item.NumberOfEmployees),
                Timezone="defult"
            };
            _db.Companies.Add(company);
            await _db.SaveChangesAsync();

            // Create Identity user as Supervisor
            var user = new User
            {
                UserName = item.Email,
                Email = item.Email,
                FirstName = item.ContactName,
                LastName = "",
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };

            var createRes = await _userManager.CreateAsync(user, item.Password);
            if (!createRes.Succeeded)
            {
                // rollback company creation or mark request as failed
                ModelState.AddModelError("", string.Join("; ", createRes.Errors.Select(e => e.Description)));
                return RedirectToAction("Details", new { id });
            }

            await _userManager.AddToRoleAsync(user, "Supervisor");

            // Link supervisor to company
            var cs = new CompanySupervisor { CompanyId = company.Id, UserId = user.Id };
            _db.CompanySupervisors.Add(cs);

            item.Status = "Approved";
            item.ReviewedAt = DateTime.UtcNow;
            item.ReviewedByAdminId = User?.Identity?.Name;

            await _db.SaveChangesAsync();

            TempData["Success"] = "Request approved and company created.";
            return RedirectToAction("Index");
        }

        [HttpPost("reject/{id:int}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reject(int id)
        {
            var item = await _db.VisitorRequests.FindAsync(id);
            if (item == null) return NotFound();

            item.Status = "Rejected";
            item.ReviewedAt = DateTime.UtcNow;
            item.ReviewedByAdminId = User?.Identity?.Name;
            await _db.SaveChangesAsync();

            TempData["Success"] = "Request rejected.";
            return RedirectToAction("Index");
        }

        private decimal CalculateRate(int n)
        {
            if (n <= 50) return 15m;
            if (n <= 100) return 10m;
            if (n <= 150) return 5m;
            return 5m;
        }
    }
}