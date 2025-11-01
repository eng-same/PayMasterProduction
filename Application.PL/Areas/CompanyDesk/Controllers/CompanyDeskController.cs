using Application.BLL.Servicies;
using Application.DAL.Data;
using Application.DAL.Models;
using Application.PL.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Application.PL.Areas.CompanyDesk.Controllers
{
    // Require authentication. If you have a distinct Company role/policy, replace this with [Authorize(Roles = "Company")] or [Authorize(Policy = "CompanyOnly")]
    [Area("CompanyDesk")]
    [Authorize]
    public class CompanyDeskController : Controller
    {
        private readonly QrCodeService _qrCodeService;
        private readonly AppDbContext _db;
        private readonly ILogger<CompanyDeskController> _logger;

        public CompanyDeskController(
            QrCodeService qrCodeService,
            AppDbContext db,
            ILogger<CompanyDeskController> logger)
        {
            _qrCodeService = qrCodeService ?? throw new ArgumentNullException(nameof(qrCodeService));
            _db = db ?? throw new ArgumentNullException(nameof(db));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<IActionResult> Index()
        {
            var companyId = GetCompanyIdFromClaims();
            if (companyId == null) return Forbid();

            var company = await _db.Companies.AsNoTracking().FirstOrDefaultAsync(c => c.Id == companyId.Value);
            var vm = new CompanyDeskVM
            {
                CompanyId = companyId.Value,
                CompanyName = company?.Name
            };

            return View(vm);
        }

        public async Task<IActionResult> CheckIn() => await QrPageForMode("in");
        public async Task<IActionResult> CheckOut() => await QrPageForMode("out");

        private async Task<IActionResult> QrPageForMode(string mode)
        {
            var companyId = GetCompanyIdFromClaims();
            if (companyId == null) return Forbid();

            // Ensure valid QR exists (creates one if missing/expired)
            var qr = await EnsureValidQrRecordAsync(companyId.Value, TimeSpan.FromMinutes(10));
            if (qr == null)
            {
                _logger.LogError("QrPageForMode: unable to obtain QR for company {CompanyId}", companyId.Value);
                return StatusCode(500, "Could not obtain QR.");
            }

            var vm = new QrPageVM { CompanyId = companyId.Value, QrId = qr.Id, Mode = mode };
            return View("QrPage", vm);
        }

        /// <summary>
        /// Return redirect to the API image endpoint for a verified, valid QR.
        /// If qrId is supplied it will be validated; otherwise a valid QR is ensured.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> QrImage(int companyId, int qrId = 0, string mode = "in", int pixelsPerModule = 6, int validMinutes = 10, bool forceRegenerate = false)
        {
            var claimCompanyId = GetCompanyIdFromClaims();
            if (claimCompanyId == null) return Forbid();
            if (claimCompanyId.Value != companyId) return Forbid();

            var company = await _db.Companies.AsNoTracking().FirstOrDefaultAsync(c => c.Id == companyId);
            if (company == null) return NotFound();
            if (!company.IsActive) return BadRequest("Company inactive.");

            // Try supplied qrId first (validate it); otherwise ensure a valid one
            CompanyQRCode qrRecord = null;
            if (qrId > 0)
            {
                qrRecord = await _qrCodeService.GetAsync(qrId);
                if (qrRecord == null || !qrRecord.IsActive || qrRecord.CompanyId != companyId || qrRecord.ExpiryDate <= DateTime.UtcNow)
                {
                    _logger.LogInformation("QrImage: provided qrId {QrId} invalid/expired/mismatch; will ensure a valid QR", qrId);
                    qrRecord = null;
                }
            }

            if (qrRecord == null)
                qrRecord = await EnsureValidQrRecordAsync(companyId, TimeSpan.FromMinutes(validMinutes), forceRegenerate);

            if (qrRecord == null)
            {
                _logger.LogError("QrImage: failed to obtain QR for company {CompanyId}", companyId);
                return StatusCode(500);
            }

            // Build absolute image URL matching API route: /api/qr/{companyId}/{qrId}/image
            var scheme = Request.Scheme;
            var host = Request.Host.ToUriComponent();
            var pathBase = Request.PathBase.HasValue ? Request.PathBase.ToUriComponent().TrimEnd('/') : "";
            var encodedMode = Uri.EscapeDataString(mode ?? "in");

            var imageUrl = $"{scheme}://{host}{pathBase}/api/qr/{companyId}/{qrRecord.Id}/image?mode={encodedMode}&pixelsPerModule={pixelsPerModule}";

            _logger.LogDebug("QrImage: redirect to {ImageUrl}", imageUrl);
            return Redirect(imageUrl);
        }

        private async Task<CompanyQRCode> EnsureValidQrRecordAsync(int companyId, TimeSpan validFor, bool force = false)
        {
            if (force)
            {
                // deactivate latest if exists, then create new
                var latest = await _db.CompanyQRCodes
                    .Where(q => q.CompanyId == companyId)
                    .OrderByDescending(q => q.GeneratedAt)
                    .FirstOrDefaultAsync();

                if (latest != null)
                {
                    latest.IsActive = false;
                    _db.CompanyQRCodes.Update(latest);
                    await _db.SaveChangesAsync();
                }

                return await _qrCodeService.CreateAsync(companyId, validFor);
            }
            else
            {
                // service will create if missing/expired
                return await _qrCodeService.RegenerateIfExpiredAsync(companyId, validFor);
            }
        }

        private int? GetCompanyIdFromClaims()
        {
            var companyClaim = User.FindFirst("companyId")?.Value;
            if (int.TryParse(companyClaim, out var companyId))
                return companyId;
            return null;
        }
    }
}
