using Application.BLL.Servicies;
using Application.PL.Services;
using Application.PL.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using QuestPDF.Helpers;
using System.IO;

namespace Application.PL.Areas.Employee.Controllers
{
    [Area("Employee")]
    [Authorize]
    public class HomeController : Controller
    {
        private readonly EmployeeService _employeeService;
        private readonly ReportRepository _reportRepository;

        public HomeController(EmployeeService employeeService, ReportRepository reportRepository)
        {
            _employeeService = employeeService;
            _reportRepository = reportRepository;
        }

        public async Task<IActionResult> Index()
        {
            var userId = User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var emp = await _employeeService.GetByUserIdAsync(userId);
            if (emp == null) return View("Error");

            var vm = await _reportRepository.GetEmployeeDashboardAsync(emp.Id);
            // populate ViewBag for layout partial
            ViewBag.EmployeeSummary = vm;
            return View(vm);
        }

        [HttpGet]
        public async Task<IActionResult> AttendanceEvents(int days = 90)
        {
            var userId = User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var emp = await _employeeService.GetByUserIdAsync(userId);
            if (emp == null) return Unauthorized();

            var events = await _reportRepository.GetAttendanceEventsAsync(emp.Id, days);
            return Json(events);
        }

        [HttpGet]
        public async Task<IActionResult> DownloadPayslip(int id)
        {
            // find salary
            var salary = await _reportRepository.GetSalaryByIdAsync(id);
            if (salary == null) return NotFound();

            var userId = User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var emp = await _employeeService.GetByUserIdAsync(userId);
            if (emp == null) return Unauthorized();

            if (salary.EmployeeId != emp.Id) return Forbid();

            // compute breakdown
            var breakdown = await _reportRepository.GetSalaryBreakdownAsync(salary.Id);

            QuestPDF.Settings.License = LicenseType.Community;

            var doc = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(20);
                    page.DefaultTextStyle(x => x.FontSize(11));

                    page.Header().Row(row =>
                    {
                        row.RelativeColumn().Column(col =>
                        {
                            col.Item().Text(salary.Employee != null ? salary.Employee.FirstName + " " + salary.Employee.LastName : "").FontSize(16).Bold();
                            col.Item().Text(salary.Employee != null ? salary.Employee.Email ?? string.Empty : string.Empty).FontSize(10).SemiBold();
                        });

                        row.ConstantColumn(160).Column(col =>
                        {
                            col.Item().Text($"Payslip: #{salary.Id}").FontSize(12).AlignRight();
                            col.Item().Text($"Pay Date: {salary.PayDate:yyyy-MM-dd}").FontSize(10).AlignRight();
                        });
                    });

                    page.Content().PaddingVertical(10).Column(column =>
                    {
                        column.Item().Text("Earnings & Deductions").FontSize(13).Bold();

                        column.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.ConstantColumn(300);
                                columns.ConstantColumn(100);
                            });

                            table.Header(header =>
                            {
                                header.Cell().Text("Description").SemiBold();
                                header.Cell().AlignRight().Text("Amount").SemiBold();
                            });

                            // base salary
                            table.Cell().Text("Base Salary");
                            table.Cell().AlignRight().Text((breakdown?.BaseAmount ?? salary.BaseAmount).ToString("C"));

                            // overtime line
                            table.Cell().Text("Overtime");
                            table.Cell().AlignRight().Text((breakdown?.TotalOvertime ?? 0m).ToString("C"));

                            // deductions line
                            table.Cell().Text("Deductions");
                            table.Cell().AlignRight().Text((breakdown?.TotalDeductions ?? 0m).ToString("C"));

                            // net
                            table.Cell().Text("Net Pay (est)").SemiBold();
                            table.Cell().AlignRight().Text((breakdown?.NetPay ?? salary.BaseAmount).ToString("C")).SemiBold();
                        });

                        column.Item().Text("\nDetails").FontSize(12).Bold();

                        if (breakdown != null)
                        {
                            if (breakdown.Overtimes != null && breakdown.Overtimes.Any())
                            {
                                column.Item().Text("Overtime entries:");
                                foreach (var ot in breakdown.Overtimes)
                                {
                                    column.Item().Text($" - {ot.Date:yyyy-MM-dd}: {ot.Hours} hrs @ {ot.Rate:C} = {(ot.Hours * ot.Rate):C}");
                                }
                            }

                            if (breakdown.Deductions != null && breakdown.Deductions.Any())
                            {
                                column.Item().Text("Deductions:");
                                foreach (var d in breakdown.Deductions)
                                {
                                    column.Item().Text($" - {d.CreatedAt:yyyy-MM-dd}: {d.Reason} = {d.Amount:C}");
                                }
                            }
                        }
                    });

                    page.Footer().AlignCenter().Text($"Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC");
                });
            });

            using var ms = new MemoryStream();
            doc.GeneratePdf(ms);
            ms.Position = 0;
            return File(ms.ToArray(), "application/pdf", $"payslip_{salary.Id}.pdf");
        }
    }
}
