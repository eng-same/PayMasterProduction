using Application.BLL.Servicies;
using Application.DAL.Data;
using Application.DAL.Models;
using Application.PL.Areas.CompanyDesk.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Linq;

namespace Application.PL.Areas.CompanyDesk.Controllers
{
    [Area("CompanyDesk")]
    [Authorize]
    public class LeaveController : Controller
    {
        private readonly AppDbContext _db;
        private readonly QrCodeService _qrCodeService;
        private readonly LiveQrService _liveQrService;

        public LeaveController(AppDbContext db, QrCodeService qrCodeService, LiveQrService liveQrService)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
            _qrCodeService = qrCodeService ?? throw new ArgumentNullException(nameof(qrCodeService));
            _liveQrService = liveQrService ?? throw new ArgumentNullException(nameof(liveQrService));
        }

        // Step 1: Employee requests a leave QR (no leave data yet). Show QR PNG only.
        public async Task<IActionResult> Create()
        {
            var companyId = await GetCompanyIdForCurrentUserAsync();
            if (companyId == null) return Forbid();

            // Create a short-lived QR that will be scanned/opened by the employee browser (authenticated)
            var qr = await _qrCodeService.CreateAsync(companyId.Value, TimeSpan.FromMinutes(10));

            var scheme = Request.Scheme;
            var host = Request.Host.ToUriComponent();
            var pathBase = Request.PathBase.HasValue ? Request.PathBase.ToUriComponent().TrimEnd('/') : "";

            // Build the URL the QR should point to: Start action which requires authentication and will validate QR
            var startUrl = $"{scheme}://{host}{pathBase}/CompanyDesk/Leave/Start?companyId={companyId.Value}&qrId={qr.Id}&token={Uri.EscapeDataString(qr.QRCodeToken)}";

            // generate PNG bytes that embed the start URL directly so scanning opens the leave form URL
            var png = _liveQrService.CreateQrPng(qr, startUrl, pixelsPerModule: 6);
            var dataUrl = "data:image/png;base64," + Convert.ToBase64String(png);

            ViewData["StartUrl"] = startUrl;
            ViewData["QrImageUrl"] = dataUrl;

            return View("Generated");
        }

        // Step 2: The employee opens the start URL (from QR). Must be signed-in. Validate QR and present the form.
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> Start(int companyId, int qrId, string token)
        {
            // ensure user is signed in
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId)) return Challenge();

            // validate QR
            var qr = await _db.CompanyQRCodes.FirstOrDefaultAsync(q => q.Id == qrId && q.CompanyId == companyId);
            if (qr == null || !qr.IsActive || qr.ExpiryDate <= DateTime.UtcNow)
            {
                ViewData["Message"] = "QR invalid or expired.";
                return View("Error");
            }

            // Some QR scanner apps append extra data to the token (for example: "<token>/?data=..." or add other query separators).
            // Clean the token parameter to extract the actual token portion before comparison.
            if (!string.IsNullOrEmpty(token))
            {
                try
                {
                    token = Uri.UnescapeDataString(token);
                }
                catch
                {
                    // ignore decode errors and use raw token
                }

                var sepIndex = token.IndexOfAny(new[] { '/', '?', '&' });
                if (sepIndex >= 0)
                    token = token.Substring(0, sepIndex);

                token = token.Trim();
            }

            if (!string.Equals(qr.QRCodeToken, token, StringComparison.Ordinal))
            {
                ViewData["Message"] = "Token mismatch.";
                return View("Error");
            }

            // Ensure the current user is an employee of the company
            var emp = await _db.Employees.FirstOrDefaultAsync(e => e.UserId == userId && e.CompanyId == companyId);
            if (emp == null)
            {
                ViewData["Message"] = "You are not recognized as an employee of this company.";
                return View("Error");
            }

            // Present the leave request form, include hidden qrId so submission can deactivate or track if needed
            var vm = new LeaveRequestCreateVm { CompanyId = companyId, EmployeeId = emp.Id };
            ViewData["QrId"] = qrId;
            return View("RequestForm", vm);
        }

        // Step 3: Employee submits the filled form. Create LeaveRequest and then generate supervisor approval QR.
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitFromQr(LeaveRequestCreateVm vm, int qrId)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId)) return Challenge();

            var companyId = await GetCompanyIdForCurrentUserAsync();
            if (companyId == null || companyId.Value != vm.CompanyId) return Forbid();

            // ensure employee matches current user
            var emp = await _db.Employees.FirstOrDefaultAsync(e => e.Id == vm.EmployeeId && e.CompanyId == vm.CompanyId && e.UserId == userId);
            if (emp == null) return Forbid();

            // create leave request (now with real data)
            var lr = new LeaveRequest
            {
                EmployeeId = vm.EmployeeId,
                LeaveType = vm.LeaveType,
                Reason = vm.Reason,
                Status = "Pending",
                SubmittedAt = DateTime.UtcNow,
                StartDate = DateTime.UtcNow.Date,
                EndDate = DateTime.UtcNow.Date
            };
            _db.LeaveRequests.Add(lr);
            await _db.SaveChangesAsync();

            // Optionally deactivate the qr used to open the form so it can't be reused
            var usedQr = await _db.CompanyQRCodes.FirstOrDefaultAsync(q => q.Id == qrId && q.CompanyId == vm.CompanyId);
            if (usedQr != null) { usedQr.IsActive = false; _db.CompanyQRCodes.Update(usedQr); await _db.SaveChangesAsync(); }

            // Do NOT generate a supervisor QR here. The supervisor should receive the request through normal UI/notifications.
            // Simply confirm to the employee that the request was submitted.
            ViewData["Message"] = "Your leave request has been submitted and is pending supervisor approval.";

            return View("Created");
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
