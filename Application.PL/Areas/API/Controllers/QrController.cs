using Application.BLL.Servicies;
using Application.DAL.Data;
using Application.DAL.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.Json;

namespace Application.PL.Areas.API.Controllers
{
    [Area("API")]
    [Route("api/[controller]")]
    [ApiController]
    public class QrController : ControllerBase
    {
        private readonly LiveQrService _liveQrService;
        private readonly QrCodeService _qrCodeService;
        private readonly AppDbContext _db;

        public QrController(
            LiveQrService liveQrService,
            QrCodeService qrCodeService,
            AppDbContext db)
        {
            _liveQrService = liveQrService ?? throw new ArgumentNullException(nameof(liveQrService));
            _qrCodeService = qrCodeService ?? throw new ArgumentNullException(nameof(qrCodeService));
            _db = db ?? throw new ArgumentNullException(nameof(db));
        }

        [HttpGet("hello")]
        public IActionResult HelloFromApi() => Ok(new { message = "hello from API" });

        public class VerifyRequest { public string TokenString { get; set; } }

        /// <summary>
        /// Verify compact token (v1.payload.sig).
        /// Requires authenticated user (Identity); prefers companyId claim but falls back to DB lookup.
        /// (unchanged)
        /// </summary>
        [HttpPost("verify")]
        [Authorize]
        public async Task<IActionResult> Verify([FromBody] VerifyRequest req)
        {
            if (req == null || string.IsNullOrWhiteSpace(req.TokenString))
                return BadRequest(new { message = "token required" });

            var parts = req.TokenString.Split('.');
            if (parts.Length != 3 || parts[0] != "v1")
                return BadRequest(new { message = "bad token format" });

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
                return BadRequest(new { message = "bad base64" });
            }

            // Verify HMAC signature using LiveQrService
            if (!_liveQrService.VerifySignature(payloadB64, sigBytes))
                return Unauthorized(new { message = "invalid signature" });

            // parse JSON payload
            JsonElement payloadJson;
            try
            {
                payloadJson = JsonSerializer.Deserialize<JsonElement>(payloadBytes);
            }
            catch
            {
                return BadRequest(new { message = "invalid payload" });
            }

            if (!payloadJson.TryGetProperty("token", out var tokenElem) ||
                !payloadJson.TryGetProperty("exp", out var expElem) ||
                !payloadJson.TryGetProperty("live", out var liveElem) ||
                !payloadJson.TryGetProperty("id", out var idElem))
            {
                return BadRequest(new { message = "missing fields" });
            }

            var token = tokenElem.GetString();
            var expIso = expElem.GetString();
            var liveIso = liveElem.GetString();
            var qrId = idElem.GetInt32();

            if (!DateTime.TryParse(expIso, null,
                    System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal,
                    out var expDt) ||
                !DateTime.TryParse(liveIso, null,
                    System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal,
                    out var liveDt))
            {
                return BadRequest(new { message = "bad timestamps" });
            }

            var now = DateTime.UtcNow;

            if (expDt < now)
                return Unauthorized(new { message = "token expired" });

            // enforce live timestamp latency
            var maxLatency = TimeSpan.FromMinutes(2);
            var delta = now - liveDt;
            if (delta < TimeSpan.Zero) delta = -delta;
            if (delta > maxLatency)
                return Unauthorized(new { message = "live timestamp outside allowed latency" });

            // load persisted QR record
            var rec = await _qrCodeService.GetAsync(qrId);
            if (rec == null)
                return Unauthorized(new { message = "token not found" });

            if (!rec.IsActive)
                return Unauthorized(new { message = "qr record inactive" });

            if (!string.Equals(rec.QRCodeToken, token, StringComparison.Ordinal))
                return Unauthorized(new { message = "token mismatch" });

            if (rec.ExpiryDate.ToUniversalTime() < now)
                return Unauthorized(new { message = "token expired (db)" });

            // replay check
            if (_liveQrService.IsLiveUsed(token, liveIso))
                return Unauthorized(new { message = "replay detected" });

            _liveQrService.MarkLiveUsed(token, liveIso, TimeSpan.FromMinutes(3));

            // === Identity / company validation ===
            // 1) get current user id
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new { message = "user not found in context" });

            // 2) try to get companyId from claims (added by ClaimsPrincipalFactory)
            var companyClaim = User.FindFirst("companyId")?.Value;
            int employeeId = 0;
            int claimCompanyId;
            if (!string.IsNullOrEmpty(companyClaim) && int.TryParse(companyClaim, out claimCompanyId))
            {
                // claim present: ensure it matches QR's company
                if (claimCompanyId != rec.CompanyId)
                    return Forbid(); //company mismatch

                // attempt to get Employee id via DB (optional)
                var emp = await _db.Employees
                    .AsNoTracking()
                    .FirstOrDefaultAsync(e => e.UserId == userId && e.CompanyId == claimCompanyId);

                if (emp != null) employeeId = emp.Id;
            }
            else
            {
                // fallback: lookup employee by UserId and verify company
                var emp = await _db.Employees
                    .AsNoTracking()
                    .FirstOrDefaultAsync(e => e.UserId == userId);

                if (emp == null)
                    return Forbid(); //employee not found

                if (emp.CompanyId != rec.CompanyId)
                    return Forbid(); //company mismatch

                employeeId = emp.Id;
            }

            // Optionally persist an attendance record if you have Attendances DbSet
            try
            {
                if (_db.Set<Attendance>() != null) // defensive: avoid compile-time dependency if Attendance missing
                {
                    var attendance = new Attendance
                    {
                        EmployeeId = employeeId,
                        //CompanyId = rec.CompanyId,
                        //QrId = rec.Id,
                        CheckInTime = now,
                        CheckOutTime = now,
                        Source = "QRScann"
                    };
                    _db.Attendances.Add(attendance);
                    await _db.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                // don't fail verification on attendance save issues; log or handle as you prefer
                // assume you have a logger wired; for now we ignore to keep core flow resilient
            }

            return Ok(new
            {
                status = "ok",
                message = "checked in",
                employeeId = employeeId,
                qrId = rec.Id,
                companyId = rec.CompanyId,
                redirectUrl = "/employee/home"
            });
        }

        /// <summary>
        /// Return QR as PNG for a stored CompanyQRCode record (simple version).
        /// </summary>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetQrPng(int id)
        {
            var record = await _qrCodeService.GetAsync(id);
            if (record == null || !record.IsActive)
                return NotFound();

            var png = _liveQrService.CreateQrPng(record);
            return File(png, "image/png");
        }

        /// <summary>
        /// New: Return QR as PNG for the specified company/id but include a baseUrl that points to the scan endpoint.
        /// Example route: GET /api/qr/{companyId}/{id}/image?mode=in&pixelsPerModule=6
        /// </summary>
        [HttpGet("{companyId}/{id}/image")]
        public async Task<IActionResult> GetQrImage(int companyId, int id, string mode = "in", int pixelsPerModule = 6)
        {
            var record = await _qrCodeService.GetAsync(id);
            if (record == null || !record.IsActive || record.CompanyId != companyId)
                return NotFound();

            var action = mode == "out" ? "ScanCheckout" : "Scan";

            // <-- Ensure the area is specified so Url.Action resolves to the CompanyDesk area controller
            var baseUrl = Url.Action(action, "QrScan", values: new { area = "CompanyDesk" }, protocol: Request.Scheme, host: Request.Host.ToUriComponent());

            var png = _liveQrService.CreateQrPng(record, baseUrl, pixelsPerModule);
            return File(png, "image/png");
        }

        /// <summary>
        /// Regenerate token for a company if expired (delegates to service).
        /// </summary>
        //[HttpPost("{companyId}/regenerate")]
        //public async Task<IActionResult> Regenerate(int companyId, [FromQuery] int validMinutes = 10)
        //{
        //    try
        //    {
        //        var qr = await _qrCodeService.RegenerateIfExpiredAsync(companyId, TimeSpan.FromMinutes(validMinutes));
        //        return Ok(new
        //        {
        //            qr.Id,
        //            qr.QRCodeToken,
        //            qr.GeneratedAt,
        //            qr.ExpiryDate
        //        });
        //    }
        //    catch (InvalidOperationException ioe)
        //    {
        //        return BadRequest(new { message = ioe.Message });
        //    }
        //    catch (Exception ex)
        //    {
        //        return StatusCode(500, new { message = "internal error", detail = ex.Message });
        //    }
        //}
        /// <summary>
        /// Ensure there is an active QR code for the company. If none exists or the latest is expired,
        /// a new QR will be generated. Optionally force a new QR even if one exists.
        /// POST /api/qr/{companyId}/ensure?validMinutes=10&force=true
        /// </summary>
        [HttpPost("{companyId}/ensure")]
        public async Task<IActionResult> Ensure(int companyId, [FromQuery] int validMinutes = 10, [FromQuery] bool force = false)
        {
            if (validMinutes <= 0) validMinutes = 10;

            // load company for validation
            var company = await _db.Companies
                .Include(c => c.CompanyQRCodes)
                .FirstOrDefaultAsync(c => c.Id == companyId);

            if (company == null)
                return NotFound(new { message = "company not found" });

            if (!company.IsActive)
                return BadRequest(new { message = "company inactive" });

            try
            {
                // If caller requested force, create a new QR regardless of existing state.
                if (force)
                {
                    // Optionally deactivate previous active QR(s)
                    var latest = company.CompanyQRCodes
                        .OrderByDescending(q => q.GeneratedAt)
                        .FirstOrDefault();

                    if (latest != null)
                    {
                        latest.IsActive = false;
                        _db.CompanyQRCodes.Update(latest);
                        await _db.SaveChangesAsync();
                    }

                    var newQr = await _qrCodeService.CreateAsync(companyId, TimeSpan.FromMinutes(validMinutes));
                    return Ok(new
                    {
                        qr = new { newQr.Id, newQr.QRCodeToken, newQr.GeneratedAt, newQr.ExpiryDate }
                    });
                }

                // Non-forced path: delegate to RegenerateIfExpiredAsync which will return an existing valid QR
                // or create a new one if missing/expired.
                var qr = await _qrCodeService.RegenerateIfExpiredAsync(companyId, TimeSpan.FromMinutes(validMinutes));
                return Ok(new
                {
                    qr = new { qr.Id, qr.QRCodeToken, qr.GeneratedAt, qr.ExpiryDate }
                });
            }
            catch (InvalidOperationException ioe)
            {
                // service-level validation (e.g. company not found/inactive)
                return BadRequest(new { message = ioe.Message });
            }
            catch (Exception ex)
            {
                // avoid leaking internals; return 500 with minimal detail
                return StatusCode(500, new { message = "internal error", detail = ex.Message });
            }
        }
    }
}

