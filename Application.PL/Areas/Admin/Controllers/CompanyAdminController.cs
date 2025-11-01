using Application.BLL.Servicies;
using Application.DAL.Data;
using Application.DAL.Models;
using Application.PL.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Application.PL.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    [Route("admin/companies")]
    public class CompanyAdminController : Controller
    {
        private readonly AppDbContext _db;
        private readonly UserManager<User> _userManager;
        private readonly QrCodeService _qrCodeService;
        private readonly ILogger<CompanyAdminController> _logger;

        public CompanyAdminController(
            AppDbContext db,
            UserManager<User> userManager,
            QrCodeService qrCodeService,
            ILogger<CompanyAdminController> logger)
        {
            _db = db;
            _userManager = userManager;
            _qrCodeService = qrCodeService;
            _logger = logger;
        }

        // GET: admin/companies
        [HttpGet("")]
        public async Task<IActionResult> Index()
        {
            var companies = await _db.Companies
                .AsNoTracking()
                .Select(c => new CompanyListItemVM
                {
                    Id = c.Id,
                    Name = c.Name,
                    IsActive = c.IsActive,
                    BillingRatePerEmployee = c.BillingRatePerEmployee,
                    ActiveEmployeeCount = c.Employees.Count(e => e.IsActive)
                })
                .ToListAsync();

            return View(companies);
        }

        [HttpGet("details/{id:int}")]
        public async Task<IActionResult> Details(int id)
        {
            // load company, employees, qrcodes, invoices, supervisors -> include user
            var company = await _db.Companies
                .AsNoTracking()
                .Include(c => c.Employees)
                .Include(c => c.CompanyQRCodes)
                .Include(c => c.Invoices)
                .Include(c => c.Supervisors)              // requires Company.Supervisors nav property and CompanySupervisor entity
                    .ThenInclude(cs => cs.User)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (company == null) return NotFound();

            // latest QR record (most recent GeneratedAt)
            var latestQr = company.CompanyQRCodes
                .OrderByDescending(q => q.GeneratedAt)
                .FirstOrDefault();

            // build absolute image url pointing to your existing API endpoint for QR PNG
            string? qrImageUrl = null;
            if (latestQr != null)
            {
                // Uses the pattern: /api/qr/{id}
                // Build absolute URL so it works in different environments.
                var scheme = Request.Scheme; // http / https
                var host = Request.Host.ToUriComponent(); // host:port
                qrImageUrl = $"{scheme}://{host}/api/qr/{latestQr.Id}";
            }

            var vm = new CompanyDetailsVM
            {
                CompanyId = company.Id,
                Name = company.Name,
                Timezone = company.Timezone,
                DefaultGraceMinutes = company.DefaultGraceMinutes,
                Address = company.Address,
                PhoneNumber = company.PhoneNumber,
                Email = company.Email,
                IsActive = company.IsActive,
                BillingRatePerEmployee = company.BillingRatePerEmployee,
                ActiveEmployeeCount = company.Employees.Count(e => e.IsActive),

                LatestQrId = latestQr?.Id,
                LatestQrToken = latestQr?.QRCodeToken,
                LatestQrGeneratedAt = latestQr?.GeneratedAt,
                LatestQrExpiryDate = latestQr?.ExpiryDate,
                LatestQrImageUrl = qrImageUrl,

                RecentInvoices = company.Invoices
                    .OrderByDescending(i => i.Date)
                    .Take(10)
                    .Select(i => new InvoiceListItemVM
                    {
                        Id = i.Id,
                        Date = i.Date,
                        ActiveEmployeeCount = i.ActiveEmployeeCount,
                        RatePerEmployee = i.RatePerEmployee,
                        TotalAmount = i.TotalAmount,
                        IsPaid = i.IsPaid
                    })
                    .ToList(),

                Supervisors = company.Supervisors?
                    .OrderBy(s => s.AssignedAt)
                    .Select(s =>
                    {
                        var u = s.User;
                        return new SupervisorVM
                        {
                            Id = s.Id,
                            UserId = s.UserId,
                            FullName = (u != null ? (u.FirstName + " " + u.LastName) : "(unknown)"),
                            Email = u?.Email ?? ""
                        };
                    }).ToList() ?? new List<SupervisorVM>()
            };

            return View(vm);
        }

        // GET: admin/companies/create
        [HttpGet("create")]
        public async Task<IActionResult> Create()
        {
            var vm = new CompanyEditVM();
            await PopulateAvailableSupervisors(vm);
            return View(vm);
        }

        // POST: admin/companies/create
        [HttpPost("create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CompanyEditVM vm)
        {
            if (!ModelState.IsValid)
            {
                await PopulateAvailableSupervisors(vm);
                return View(vm);
            }

            var company = new Company
            {
                Name = vm.Name,
                Timezone = vm.Timezone,
                DefaultGraceMinutes = vm.DefaultGraceMinutes,
                Address = vm.Address,
                Email = vm.Email,
                PhoneNumber = vm.PhoneNumber,
                IsActive = vm.IsActive,
                BillingRatePerEmployee = vm.BillingRatePerEmployee
            };

            _db.Companies.Add(company);
            await _db.SaveChangesAsync();

            // Assign supervisors if any
            if (vm.SelectedSupervisorIds?.Any() == true)
            {
                var supervisors = vm.SelectedSupervisorIds.Select(uid => new CompanySupervisor
                {
                    CompanyId = company.Id,
                    UserId = uid
                });
                _db.CompanySupervisors.AddRange(supervisors);
                await _db.SaveChangesAsync();
            }

            // Generate initial QR token
            try
            {
                var qr = await _qrCodeService.CreateAsync(company.Id, TimeSpan.FromDays(7));
                _logger.LogInformation("Created initial QR for company {CompanyId} QR#{QrId}", company.Id, qr.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create initial QR for company {CompanyId}", company.Id);
                // We don't fail the entire create if QR fails; optionally show a warning to admin
                TempData["Warning"] = "Company created but initial QR generation failed. Check logs.";
            }

            // Optionally create an initial invoice snapshot if billing rate and employees exist
            await MaybeCreateInvoiceSnapshot(company.Id);

            return RedirectToAction(nameof(Index));
        }

        // GET: admin/companies/edit/{id}
        [HttpGet("edit/{id:int}")]
        public async Task<IActionResult> Edit(int id)
        {
            var company = await _db.Companies
                .Include(c => c.Supervisors)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (company == null) return NotFound();

            var vm = new CompanyEditVM
            {
                Id = company.Id,
                Name = company.Name,
                Timezone = company.Timezone,
                DefaultGraceMinutes = company.DefaultGraceMinutes,
                Address = company.Address,
                PhoneNumber = company.PhoneNumber,
                Email = company.Email,
                BillingRatePerEmployee = company.BillingRatePerEmployee,
                IsActive = company.IsActive,
                SelectedSupervisorIds = company.Supervisors?.Select(s => s.UserId).ToList() ?? new List<string>()
            };

            await PopulateAvailableSupervisors(vm);
            return View(vm);
        }

        // POST: admin/companies/edit/{id}
        [HttpPost("edit/{id:int}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, CompanyEditVM vm)
        {
            if (!ModelState.IsValid)
            {
                await PopulateAvailableSupervisors(vm);
                return View(vm);
            }

            var company = await _db.Companies
                .Include(c => c.Supervisors)
                .Include(c => c.Employees)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (company == null) return NotFound();

            var previousRate = company.BillingRatePerEmployee;

            company.Name = vm.Name;
            company.Timezone = vm.Timezone;
            company.DefaultGraceMinutes = vm.DefaultGraceMinutes;
            company.Address = vm.Address;
            company.PhoneNumber = vm.PhoneNumber;
            company.Email = vm.Email;
            company.IsActive = vm.IsActive;
            company.BillingRatePerEmployee = vm.BillingRatePerEmployee;

            // Update supervisors: simple approach - remove all and re-add (you can optimize)
            var existing = company.Supervisors.ToList();
            _db.CompanySupervisors.RemoveRange(existing);

            if (vm.SelectedSupervisorIds?.Any() == true)
            {
                var newSupervisors = vm.SelectedSupervisorIds
                    .Select(uid => new CompanySupervisor { CompanyId = company.Id, UserId = uid });
                await _db.CompanySupervisors.AddRangeAsync(newSupervisors);
            }

            await _db.SaveChangesAsync();

            // If billing rate was set or changed, create invoice snapshot automatically
            if (vm.BillingRatePerEmployee.HasValue && vm.BillingRatePerEmployee != previousRate)
            {
                await MaybeCreateInvoiceSnapshot(company.Id);
            }

            TempData["Success"] = "Company updated.";
            return RedirectToAction(nameof(Index));
        }

        // POST: admin/companies/{id}/generate-qr
        [HttpPost("{id:int}/generate-qr")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GenerateQr(int id)
        {
            var company = await _db.Companies.FindAsync(id);
            if (company == null) return NotFound();

            try
            {
                var qr = await _qrCodeService.CreateAsync(company.Id, TimeSpan.FromDays(7));
                TempData["Success"] = $"New QR generated (id={qr.Id}).";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create QR for company {CompanyId}", id);
                TempData["Error"] = "Failed to generate QR.";
            }

            return RedirectToAction(nameof(Details), new { id });
        }

        // POST: admin/companies/{id}/create-invoice
        [HttpPost("{id:int}/create-invoice")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateInvoice(int id)
        {
            await MaybeCreateInvoiceSnapshot(id);
            return RedirectToAction(nameof(Details), new { id }); // maybe later show invoice there
        }

        // Helper: fill AvailableSupervisors list
        private async Task PopulateAvailableSupervisors(CompanyEditVM vm)
        {
            var users = await _userManager.Users
                .OrderBy(u => u.UserName)
                .Select(u => new { u.Id, Display = u.FirstName + " " + u.LastName + " (" + u.Email + ")" })
                .ToListAsync();

            vm.AvailableSupervisors = users
                .Select(u => new SelectListItem(u.Display, u.Id))
                .ToList();
        }

        // Helper: create Invoice snapshot using current active employee count and company billing rate
        private async Task MaybeCreateInvoiceSnapshot(int companyId)
        {
            var company = await _db.Companies
                .Include(c => c.Employees)
                .FirstOrDefaultAsync(c => c.Id == companyId);

            if (company == null) return;

            if (!company.BillingRatePerEmployee.HasValue) return;

            var activeCount = company.Employees.Count(e => e.IsActive);
            var rate = company.BillingRatePerEmployee.Value;
            var total = rate * activeCount;

            var invoice = new Invoice
            {
                CompanyId = company.Id,
                ActiveEmployeeCount = activeCount,
                RatePerEmployee = rate,
                TotalAmount = total,
                Date = DateTime.UtcNow
            };

            _db.invoices.Add(invoice);
            await _db.SaveChangesAsync();
        }

    }
}
