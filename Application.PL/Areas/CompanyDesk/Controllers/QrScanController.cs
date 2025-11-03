using Application.BLL.Servicies;
using Application.DAL.Data;
using Application.DAL.Models;
using Application.PL.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using EmployeeModel = Application.DAL.Models.Employee;

namespace Application.PL.Areas.CompanyDesk.Controllers
{
    [Area("CompanyDesk")]
    [Authorize]
    public class QrScanController : Controller
    {
        private readonly LiveQrService _liveQrService;
        private readonly QrCodeService _qrCodeService;
        private readonly AppDbContext _db;
        private readonly ILogger<QrScanController> _logger;

        public QrScanController(LiveQrService liveQrService, QrCodeService qrCodeService, AppDbContext db, ILogger<QrScanController> logger)
        {
            _liveQrService = liveQrService ?? throw new ArgumentNullException(nameof(liveQrService));
            _qrCodeService = qrCodeService ?? throw new ArgumentNullException(nameof(qrCodeService));
            _db = db ?? throw new ArgumentNullException(nameof(db));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        [HttpGet]
        public async Task<IActionResult> Scan(string data) => await ProcessScan(data, isCheckout: false);

        [HttpGet]
        public async Task<IActionResult> ScanCheckout(string data) => await ProcessScan(data, isCheckout: true);

        private async Task<IActionResult> ProcessScan(string data, bool isCheckout)
        {
            if (string.IsNullOrWhiteSpace(data))
                return View("Result", new QrResultVm { Success = false, Message = "Missing data parameter." });

            try
            {
                var parseResult = ParseAndValidateToken(data);
                if (!parseResult.IsValid)
                    return View("Result", new QrResultVm { Success = false, Message = parseResult.ErrorMessage });

                // parsed payload values
                var token = parseResult.Token!;
                var liveIso = parseResult.LiveIso!;    // ORIGINAL ISO string embedded in the token
                var liveDto = parseResult.Live!;
                var expDto = parseResult.Exp!;
                var qrId = parseResult.QrId;

                var now = DateTimeOffset.UtcNow;
                //if (expDto < now)
                //    return View("Result", new QrResultVm { Success = false, Message = "Token expired." });

                // small latency guard using parsed DateTimeOffset
                var maxLatency = TimeSpan.FromMinutes(15);
                var delta = now - liveDto;
                if (delta < TimeSpan.Zero) delta = -delta;
                if (delta > maxLatency)
                    return View("Result", new QrResultVm { Success = false, Message = "Live timestamp outside allowed latency." });

                // load persisted QR record and validate token match & expiry
                var rec = await _qrCodeService.GetAsync(qrId);
                if (rec == null || !rec.IsActive)
                    return View("Result", new QrResultVm { Success = false, Message = "QR not found or inactive." });

                if (!string.Equals(rec.QRCodeToken, token, StringComparison.Ordinal))
                    return View("Result", new QrResultVm { Success = false, Message = "Token mismatch." });

                //if (rec.ExpiryDate.ToUniversalTime() < now.UtcDateTime)
                //    return View("Result", new QrResultVm { Success = false, Message = "QR expired." });

                // replay protection - use original liveIso string (no ToString())
                //if (_liveQrService.IsLiveUsed(token, liveIso))
                //    return View("Result", new QrResultVm { Success = false, Message = "Replay detected." });

                // mark as used using the original liveIso string
                _liveQrService.MarkLiveUsed(token, liveIso, TimeSpan.FromMinutes(3));

                // Identity resolution and company check
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return View("Result", new QrResultVm { Success = false, Message = "User not authenticated." });

                int? companyClaimId = null;
                if (int.TryParse(User.FindFirst("companyId")?.Value, out var cid))
                    companyClaimId = cid;

                EmployeeModel emp = null;
                if (companyClaimId.HasValue)
                {
                    emp = await _db.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.UserId == userId && e.CompanyId == companyClaimId.Value);
                    if (emp == null)
                        return View("Result", new QrResultVm { Success = false, Message = "Employee not found for company claim." });

                    if (emp.CompanyId != rec.CompanyId)
                        return View("Result", new QrResultVm { Success = false, Message = "Company mismatch." });
                }
                else
                {
                    emp = await _db.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.UserId == userId);
                    if (emp == null)
                        return View("Result", new QrResultVm { Success = false, Message = "Employee not found." });

                    if (emp.CompanyId != rec.CompanyId)
                        return View("Result", new QrResultVm { Success = false, Message = "Company mismatch." });
                }

                // Handle check-in / check-out
                var resultVm = await PerformAttendanceAction(emp.Id, rec.CompanyId, isCheckout);
                return View("Result", resultVm);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ProcessScan: unexpected error");
                return View("Result", new QrResultVm { Success = false, Message = "Internal error: " + ex.Message });
            }
        }

        private (bool IsValid, string? ErrorMessage, string? Token, string? LiveIso, DateTimeOffset? Live, DateTimeOffset? Exp, int QrId)
    ParseAndValidateToken(string rawData)
        {
            try
            {
                // URL-decode first
                var decoded = Uri.UnescapeDataString(rawData.Trim());

                var parts = decoded.Split('.');
                if (parts.Length != 3 || parts[0] != "v1")
                    return (false, "Invalid token format.", null, null, null, null, 0);

                var payloadB64 = parts[1];
                var sigB64 = parts[2];

                byte[] payloadBytes, sigBytes;
                try
                {
                    payloadBytes = Base64Url.Decode(payloadB64);
                    sigBytes = Base64Url.Decode(sigB64);
                }
                catch
                {
                    return (false, "Bad base64 in token.", null, null, null, null, 0);
                }

                // verify signature
                if (!_liveQrService.VerifySignature(payloadB64, sigBytes))
                    return (false, "Invalid signature.", null, null, null, null, 0);

                JsonElement payloadJson;
                try
                {
                    payloadJson = JsonSerializer.Deserialize<JsonElement>(payloadBytes);
                }
                catch
                {
                    return (false, "Invalid payload JSON.", null, null, null, null, 0);
                }

                if (!payloadJson.TryGetProperty("token", out var tokenElem) ||
                    !payloadJson.TryGetProperty("exp", out var expElem) ||
                    !payloadJson.TryGetProperty("live", out var liveElem) ||
                    !payloadJson.TryGetProperty("id", out var idElem))
                {
                    return (false, "Payload missing required fields.", null, null, null, null, 0);
                }

                var token = tokenElem.GetString();
                var expIso = expElem.GetString();
                var liveIso = liveElem.GetString(); // keep the original ISO string
                var qrId = idElem.GetInt32();

                if (!DateTimeOffset.TryParseExact(expIso, "o", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var expDto) ||
                    !DateTimeOffset.TryParseExact(liveIso, "o", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var liveDto))
                {
                    return (false, "Bad timestamps.", null, null, null, null, 0);
                }

                // Return both the original ISO string and parsed DateTimeOffset values
                return (true, null, token, liveIso, liveDto, expDto, qrId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "ParseAndValidateToken failed");
                return (false, "Token parse error.", null, null, null, null, 0);
            }
        }

        private async Task<QrResultVm> PerformAttendanceAction(int employeeId, int companyId, bool isCheckout)
        {
            var now = DateTime.UtcNow;

            if (!isCheckout)
            {
                // Prevent multiple open check-ins: if there's already an open attendance, do not create another
                var existingOpen = await _db.Attendances
                    .Where(a => a.EmployeeId == employeeId && a.CheckOutTime == null)
                    .OrderByDescending(a => a.CheckInTime)
                    .FirstOrDefaultAsync();

                if (existingOpen != null)
                {
                    return new QrResultVm
                    {
                        Success = false,
                        Message = "You are already checked in. Please check out before checking in again.",
                        EmployeeId = employeeId,
                        CompanyId = companyId,
                        AttendanceId = existingOpen.Id,
                        IsCheckout = false
                    };
                }

                var attendance = new Attendance
                {
                    EmployeeId = employeeId,
                    CheckInTime = now,
                    Source = "QRScan"
                };
                _db.Attendances.Add(attendance);
                await _db.SaveChangesAsync();

                return new QrResultVm
                {
                    Success = true,
                    Message = "Checked in successfully.",
                    EmployeeId = employeeId,
                    CompanyId = companyId,
                    AttendanceId = attendance.Id,
                    IsCheckout = false
                };
            }
            else
            {
                var open = await _db.Attendances
                    .Where(a => a.EmployeeId == employeeId && a.CheckOutTime == null)
                    .OrderByDescending(a => a.CheckInTime)
                    .FirstOrDefaultAsync();

                if (open != null)
                {
                    open.CheckOutTime = now;
                    open.Source = (open.Source ?? "") + "|QRScan-Checkout";
                    _db.Attendances.Update(open);
                    await _db.SaveChangesAsync();

                    return new QrResultVm
                    {
                        Success = true,
                        Message = "Checked out successfully (matched open session).",
                        EmployeeId = employeeId,
                        CompanyId = companyId,
                        AttendanceId = open.Id,
                        IsCheckout = true
                    };
                }

                // If no open session exists, do not create a fallback that looks like a valid attendance.
                return new QrResultVm
                {
                    Success = false,
                    Message = "No open check-in session found to check out from.",
                    EmployeeId = employeeId,
                    CompanyId = companyId,
                    AttendanceId = null,
                    IsCheckout = true
                };
            }
        }
    }

}
