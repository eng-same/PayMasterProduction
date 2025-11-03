using Application.PL.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Application.PL.Areas.Supervisor.Controllers
{
    [Area("Supervisor")]
    [Authorize]
    public class ReportsController : Controller
    {
        private readonly ReportRepository _repo;
        private readonly UserManager<Application.DAL.Models.User> _userManager;

        public ReportsController(ReportRepository repo, UserManager<Application.DAL.Models.User> userManager)
        {
            _repo = repo;
            _userManager = userManager;
        }

        public IActionResult Index()
        {
            return View("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ExportPdf()
        {
            // Get the company for the current supervisor
            var userId = _userManager.GetUserId(User);
            var company = await _repo.GetCompanyForSupervisorAsync(userId);
            if (company == null)
            {
                return Forbid();
            }

            var dashboard = await _repo.GetCompanyDashboardAsync(company.Id);

            QuestPDF.Settings.License = LicenseType.Community;

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(2, Unit.Centimetre);
                    page.DefaultTextStyle(x => x.FontSize(11));

                    page.Header().Row(row =>
                    {
                        row.RelativeColumn().Column(col =>
                        {
                            col.Item().Text(company.Name).FontSize(18).Bold();
                            col.Item().Text(company.Address ?? string.Empty).FontSize(10).SemiBold();
                        });

                        row.ConstantColumn(120).Column(col =>
                        {
                            col.Item().Text($"Generated: {System.DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC").FontSize(10).AlignRight();
                            col.Item().Text($"Supervisor: {User.Identity?.Name}").FontSize(10).AlignRight();
                        });
                    });

                    page.Content().PaddingVertical(10).Column(column =>
                    {
                        column.Item().Text("Summary").FontSize(14).Bold().Underline();

                        column.Item().Row(r =>
                        {
                            r.RelativeColumn().Column(c =>
                            {
                                c.Item().Text($"Total Employees: {dashboard?.TotalEmployees ?? 0}");
                                c.Item().Text($"Total Unpaid Amount: {(dashboard?.TotalUnpaidAmount ?? 0m):C}");
                            });

                            r.ConstantColumn(160).Column(c =>
                            {
                                c.Item().Text("Monthly Attendance (last points)").Bold();
                                var points = dashboard?.MonthlyAttendance;
                                if (points != null && points.Any())
                                {
                                    c.Item().Text(string.Join(", ", points.Take(6).Select(p => p?.ToString() ?? "-")));
                                }
                                else
                                {
                                    c.Item().Text("(no data)");
                                }
                            });
                        });

                        column.Item().Text(" ");

                        column.Item().Text("Recent Unpaid Invoices").FontSize(12).Bold();

                        var invoices = dashboard?.RecentUnpaidInvoices;

                        if (invoices != null && invoices.Any())
                        {
                            column.Item().Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.ConstantColumn(60);
                                    columns.RelativeColumn();
                                    columns.ConstantColumn(80);
                                });

                                table.Header(header =>
                                {
                                    header.Cell().Element(CellStyle).Text("ID").FontSize(10).SemiBold();
                                    header.Cell().Element(CellStyle).Text("Description").FontSize(10).SemiBold();
                                    header.Cell().Element(CellStyle).Text("Amount").FontSize(10).SemiBold();
                                });

                                // RecentUnpaidInvoiceVM structure isn't shown; try to reflectively pull common properties
                                foreach (var inv in invoices)
                                {
                                    var idProp = inv?.GetType().GetProperty("InvoiceId") ?? inv?.GetType().GetProperty("Id");
                                    var descProp = inv?.GetType().GetProperty("Description") ?? inv?.GetType().GetProperty("Title");
                                    var amountProp = inv?.GetType().GetProperty("Amount") ?? inv?.GetType().GetProperty("Total");

                                    var idVal = idProp != null ? idProp.GetValue(inv)?.ToString() : "-";
                                    var descVal = descProp != null ? descProp.GetValue(inv)?.ToString() : inv?.ToString() ?? "-";
                                    var amountVal = amountProp != null ? amountProp.GetValue(inv) : null;

                                    table.Cell().Element(CellStyle).Text(idVal).FontSize(9);
                                    table.Cell().Element(CellStyle).Text(descVal).FontSize(9);
                                    table.Cell().Element(CellStyle).Text(amountVal != null ? string.Format("{0:C}", amountVal) : "-").FontSize(9).AlignRight();
                                }

                                static IContainer CellStyle(IContainer container)
                                {
                                    return container.PaddingVertical(4).PaddingHorizontal(6);
                                }
                            });
                        }
                        else
                        {
                            column.Item().Text("No unpaid invoices.").Italic();
                        }

                        column.Item().Text(" ");

                        column.Item().Text("Employee Reports / Absentees").FontSize(12).Bold();
                        var employees = dashboard?.EmployeeReports;
                        if (employees != null && employees.Any())
                        {
                            column.Item().Column(c =>
                            {
                                foreach (var emp in employees.Take(10))
                                {
                                    var nameProp = emp?.GetType().GetProperty("Name") ?? emp?.GetType().GetProperty("FullName");
                                    var val = nameProp != null ? nameProp.GetValue(emp)?.ToString() : emp?.ToString() ?? "-";
                                    c.Item().Text("- " + val).FontSize(10);
                                }
                            });
                        }
                        else
                        {
                            column.Item().Text("No employee report data.").Italic();
                        }
                    });

                    page.Footer().AlignCenter().Text(x => x.CurrentPageNumber());
                });
            });

            using var ms = new MemoryStream();
            document.GeneratePdf(ms);
            var bytes = ms.ToArray();

            return File(bytes, "application/pdf", $"company-report-{company.Id}.pdf");
        }

        // Simple view action for viewing a single report in the browser
        public IActionResult ViewReport(int id = 0)
        {
            // For now return a placeholder view. In production, load data by id and render it.
            ViewData["ReportId"] = id;
            return View("ViewReport");
        }
    }
}
