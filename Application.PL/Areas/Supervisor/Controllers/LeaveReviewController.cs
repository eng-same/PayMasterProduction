using Application.DAL.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Application.PL.Areas.Supervisor.Controllers
{
    [Area("Supervisor")]
    [Authorize]
    public class LeaveReviewController : Controller
    {
        private readonly AppDbContext _db;

        public LeaveReviewController(AppDbContext db)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
        }

        // Index: list leave requests for company the supervisor manages
        public async Task<IActionResult> Index()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId)) return Forbid();

            var sup = await _db.CompanySupervisors.AsNoTracking().FirstOrDefaultAsync(s => s.UserId == userId);
            if (sup == null) return Forbid();

            var leaves = await _db.LeaveRequests
                .Include(l => l.Employee)
                .Where(l => l.Employee != null && l.Employee.CompanyId == sup.CompanyId)
                .OrderByDescending(l => l.SubmittedAt)
                .ToListAsync();

            return View(leaves);
        }

        public async Task<IActionResult> Review(int reqId, int qrId)
        {
            // Validate supervisor's company via CompanySupervisor or via claim
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

            var lr = await _db.LeaveRequests.Include(l => l.Employee).FirstOrDefaultAsync(l => l.Id == reqId);
            if (lr == null) return NotFound();

            // Validate QR exists and active
            var qr = await _db.CompanyQRCodes.FirstOrDefaultAsync(q => q.Id == qrId);
            if (qr == null || !qr.IsActive || qr.ExpiryDate <= DateTime.UtcNow)
            {
                ViewData["Message"] = "QR invalid or expired.";
                return View("Status");
            }

            // optionally check that the supervisor manages that company
            // For now just show the request
            ViewData["LeaveRequest"] = lr;
            ViewData["QrId"] = qrId;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(int reqId, int qrId)
        {
            var lr = await _db.LeaveRequests.FirstOrDefaultAsync(l => l.Id == reqId);
            if (lr == null) return NotFound();

            lr.Status = "Approved";
            _db.LeaveRequests.Update(lr);

            // Mark related attendance entries within the leave period as excused so missed minutes not counted
            try
            {
                var start = lr.StartDate.Date;
                var end = lr.EndDate.Date.AddDays(1).AddTicks(-1);

                var attendances = await _db.Attendances
                    .Where(a => a.EmployeeId == lr.EmployeeId && a.CheckInTime >= start && a.CheckInTime <= end)
                    .ToListAsync();

                foreach (var a in attendances)
                {
                    a.IsExcused = true;
                }

                _db.Attendances.UpdateRange(attendances);
            }
            catch
            {
                // ignore attendance update failures to avoid blocking approval; log if you have a logger
            }

            await _db.SaveChangesAsync();

            // deactivate QR to prevent reuse
            var qr = await _db.CompanyQRCodes.FirstOrDefaultAsync(q => q.Id == qrId);
            if (qr != null) { qr.IsActive = false; _db.CompanyQRCodes.Update(qr); await _db.SaveChangesAsync(); }

            ViewData["Message"] = "Request approved.";
            return View("Status");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reject(int reqId, int qrId)
        {
            var lr = await _db.LeaveRequests.FirstOrDefaultAsync(l => l.Id == reqId);
            if (lr == null) return NotFound();
            lr.Status = "Rejected";
            _db.LeaveRequests.Update(lr);
            await _db.SaveChangesAsync();

            var qr = await _db.CompanyQRCodes.FirstOrDefaultAsync(q => q.Id == qrId);
            if (qr != null) { qr.IsActive = false; _db.CompanyQRCodes.Update(qr); await _db.SaveChangesAsync(); }

            ViewData["Message"] = "Request rejected.";
            return View("Status");
        }
    }
}
