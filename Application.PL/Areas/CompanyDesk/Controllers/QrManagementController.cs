using Application.BLL.Servicies;
using Application.DAL.Data;
using Application.DAL.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Application.PL.Areas.CompanyDesk.Controllers
{
    [Area("CompanyDesk")]
    [Authorize]
    public class QrManagementController : Controller
    {
        private readonly AppDbContext _db;
        private readonly QrCodeService _qrCodeService;

        public QrManagementController(AppDbContext db, QrCodeService qrCodeService)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
            _qrCodeService = qrCodeService ?? throw new ArgumentNullException(nameof(qrCodeService));
        }

        public async Task<IActionResult> Index()
        {
            var companyId = await GetCompanyIdForCurrentUserAsync();
            if (companyId == null) return Forbid();

            var qrs = await _db.CompanyQRCodes
                .Where(q => q.CompanyId == companyId.Value)
                .OrderByDescending(q => q.GeneratedAt)
                .ToListAsync();

            return View(qrs);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForceRegenerate(int minutes = 10)
        {
            var companyId = await GetCompanyIdForCurrentUserAsync();
            if (companyId == null) return Forbid();

            var qr = await _qrCodeService.CreateAsync(companyId.Value, TimeSpan.FromMinutes(minutes));
            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Deactivate(int id)
        {
            var companyId = await GetCompanyIdForCurrentUserAsync();
            if (companyId == null) return Forbid();

            var qr = await _db.CompanyQRCodes.FirstOrDefaultAsync(q => q.Id == id && q.CompanyId == companyId.Value);
            if (qr == null) return NotFound();
            qr.IsActive = false;
            _db.CompanyQRCodes.Update(qr);
            await _db.SaveChangesAsync();
            return RedirectToAction("Index");
        }

        private async Task<int?> GetCompanyIdForCurrentUserAsync()
        {
            var companyClaim = User.FindFirst("companyId")?.Value;
            if (int.TryParse(companyClaim, out var companyId))
                return companyId;

            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId)) return null;

            var sup = await _db.CompanySupervisors.AsNoTracking().FirstOrDefaultAsync(s => s.UserId == userId);
            if (sup != null) return sup.CompanyId;

            return null;
        }
    }
}
