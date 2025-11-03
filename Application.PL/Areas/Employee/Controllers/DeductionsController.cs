using Application.DAL.Models;
using Application.PL.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Application.PL.Areas.Employee.Controllers
{
    [Area("Employee")]
    [Authorize]
    public class DeductionsController : Controller
    {
        private readonly ReportRepository _repo;
        private readonly ILogger<DeductionsController> _logger;

        public DeductionsController(ReportRepository repo, ILogger<DeductionsController> logger)
        {
            _repo = repo;
            _logger = logger;
        }

        // List all deductions for current employee
        public async Task<IActionResult> Index()
        {
            var userId = User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            // resolve employee id via repo or service - assume a helper exists in ReportRepository to map user -> employee
            var emp = await _repo.GetEmployeeByUserIdAsync(userId);
            if (emp == null) return Forbid();

            var list = await _repo.GetDeductionsForEmployeeAsync(emp.Id);
            return View("Index", list);
        }

        [HttpGet]
        public async Task<IActionResult> DownloadPdf(int id)
        {
            var ded = await _repo.GetDeductionByIdAsync(id);
            if (ded == null) return NotFound();

            QuestPDF.Settings.License = LicenseType.Community;

            var doc = Document.Create(x =>
            {
                x.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(20);
                    page.Header().Text($"Deduction #{ded.Id}").FontSize(16).Bold();

                    page.Content().Column(col =>
                    {
                        col.Item().Text($"Employee: {ded.Employee?.FirstName} {ded.Employee?.LastName}");
                        col.Item().Text($"Date: {ded.CreatedAt:yyyy-MM-dd}");
                        col.Item().Text($"Reason: {ded.Reason}");
                        col.Item().Text($"Minutes deducted: {ded.MinutesDeducted}");
                        col.Item().Text($"Amount: {ded.Amount:C}");
                    });

                    page.Footer().AlignCenter().Text($"Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC");
                });
            });

            using var ms = new MemoryStream();
            doc.GeneratePdf(ms);
            ms.Position = 0;
            return File(ms.ToArray(), "application/pdf", $"deduction_{ded.Id}.pdf");
        }
    }
}
