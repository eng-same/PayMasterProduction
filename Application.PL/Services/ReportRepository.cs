using Application.DAL.Data;
using Application.DAL.Models;
using Application.PL.ViewModels;
using Microsoft.EntityFrameworkCore;
using System.Globalization;


namespace Application.PL.Services
{
    public class ReportRepository {

        private readonly AppDbContext _context;
        public ReportRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<bool> MarkInvoicePaidAsync(int invoiceId)
        {
            var inv = await _context.invoices.FindAsync(invoiceId);
            if (inv == null) return false;
            inv.IsPaid = true;
            inv.PaidAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<DashboardVM> GetDashboardAsync(int months = 6, int topCompanies = 5, int topLateEmployees = 5)
        {
            var now = DateTime.UtcNow;
            // start from the first day of the month "months-1" months ago
            var startDate = new DateTime(now.Year, now.Month, 1).AddMonths(-(months - 1));
            var endDate = now;

            // Totals
            var totalCompanies = await _context.Companies.CountAsync();
            var totalEmployees = await _context.Employees.CountAsync();
            var totalActiveEmployees = await _context.Employees.CountAsync(e => e.IsActive);


            var totalInvoices = await _context.invoices.CountAsync();
            var totalOutstandingAmount = await _context.invoices.Where(i => !i.IsPaid).SumAsync(i => (decimal?)i.TotalAmount) ?? 0m;

            // Monthly chart: invoiced total and paid total grouped by year-month
            var monthlyRaw = await _context.invoices
            .Where(i => i.Date >= startDate && i.Date <= endDate)
            .GroupBy(i => new { i.Date.Year, i.Date.Month })
            .Select(g => new MonthlyPoint
            {
                Year = g.Key.Year,
                Month = g.Key.Month,
                Invoiced = g.Sum(x => x.TotalAmount),
                Paid = g.Where(x => x.IsPaid).Sum(x => x.TotalAmount)
            })
            .ToListAsync();

            var monthly = new List<MonthlyPoint>();
            for (int i = 0; i < months; i++)
            {
                var dt = startDate.AddMonths(i);
                var found = monthlyRaw.FirstOrDefault(m => m.Year == dt.Year && m.Month == dt.Month);
                monthly.Add(found ?? new MonthlyPoint { Year = dt.Year, Month = dt.Month, Invoiced = 0m, Paid = 0m });
            }

            // Companies with unpaid invoices and their unpaid totals
            var companiesWithUnpaid = await _context.invoices
            .Where(i => !i.IsPaid)
            .GroupBy(i => new { i.CompanyId, i.Company.Name })
            .Select(g => new TopUnpaidCompanyVM
            {
                CompanyId = g.Key.CompanyId,
                CompanyName = g.Key.Name,
                UnpaidAmount = g.Sum(x => x.TotalAmount),
                UnpaidInvoiceCount = g.Count()
            })
            .OrderByDescending(x => x.UnpaidAmount)
            .Take(topCompanies)
            .ToListAsync();

            // Employees with most payroll deductions (late/absences) - useful to spot trouble
            var topLateEmployeesList = await _context.PayrollDeductions
            .GroupBy(d => new { d.EmployeeId, d.Employee.FirstName, d.Employee.LastName, d.Employee.CompanyId })
            .Select(g => new
            {
                EmployeeId = g.Key.EmployeeId,
                FullName = g.Key.FirstName + " " + g.Key.LastName,
                CompanyId = g.Key.CompanyId,
                TotalAmount = g.Sum(x => x.Amount),
                TotalMinutes = g.Sum(x => x.MinutesDeducted)
            })
            .OrderByDescending(x => x.TotalMinutes)
            .Take(topLateEmployees)
            .ToListAsync();


            var topLateEmployeesVM = topLateEmployeesList.Select(x => new TopLateEmployeeVM
            {
                EmployeeId = x.EmployeeId,
                FullName = x.FullName,
                CompanyId = x.CompanyId,
                TotalMinutesDeducted = x.TotalMinutes,
                TotalAmountDeducted = x.TotalAmount
            }).ToList();

            // Quick attendance stats: total check-ins in range
            var totalAttendances = await _context.Attendances.Where(a => a.CheckInTime >= startDate && a.CheckInTime <= endDate).CountAsync();
            var avgAttendancePerCompany = 0m;
            if (totalCompanies > 0)
            {
                var avg = await _context.Attendances
                .Where(a => a.CheckInTime >= startDate && a.CheckInTime <= endDate)
                .GroupBy(a => a.Employee.CompanyId)
                .Select(g => g.Count())
                .AverageAsync();
                avgAttendancePerCompany = (decimal)avg;
            }

            return new DashboardVM
            {
                TotalCompanies = totalCompanies,
                TotalEmployees = totalEmployees,
                TotalActiveEmployees = totalActiveEmployees,
                TotalInvoices = totalInvoices,
                TotalOutstandingAmount = totalOutstandingAmount,


                MonthlyPoints = monthly,
                CompaniesWithUnpaidInvoices = companiesWithUnpaid,
                TopLateEmployees = topLateEmployeesVM,


                TotalAttendances = totalAttendances,
                AvgAttendancePerCompany = avgAttendancePerCompany
            };
        }

        // Return the Company entity for a supervisor user (null if not a supervisor)
        public async Task<Company> GetCompanyForSupervisorAsync(string userId)
        {
            if (string.IsNullOrEmpty(userId)) return null;
            var sup = await _context.CompanySupervisors
                .Include(s => s.Company)
                .FirstOrDefaultAsync(s => s.UserId == userId);
            return sup?.Company;
        }

        // Company dashboard: unpaid invoices, monthly attendance, employee totals, absentees
        public async Task<CompanyDashboardVM> GetCompanyDashboardAsync(int companyId, int months = 6)
        {
            var now = DateTime.UtcNow;
            var startDate = new DateTime(now.Year, now.Month, 1).AddMonths(-(months - 1));
            var endDate = now;

            var company = await _context.Companies.FindAsync(companyId);
            if (company == null) return null;

            var totalEmployees = await _context.Employees.CountAsync(e => e.CompanyId == companyId);

            var totalUnpaidAmount = await _context.invoices
                .Where(i => i.CompanyId == companyId && !i.IsPaid)
                .SumAsync(i => (decimal?)i.TotalAmount) ?? 0m;

            var recentUnpaid = await _context.invoices
                .Where(i => i.CompanyId == companyId && !i.IsPaid)
                .OrderByDescending(i => i.Date)
                .Take(10)
                .Select(i => new RecentUnpaidInvoiceVM
                {
                    InvoiceId = i.Id,
                    Date = i.Date,
                    Amount = i.TotalAmount,
                    Notes = i.Notes
                })
                .ToListAsync();

            // monthly attendance count (check-ins) grouped by year/month
            var monthlyAttendanceRaw = await _context.Attendances
                .Where(a => a.Employee.CompanyId == companyId && a.CheckInTime >= startDate && a.CheckInTime <= endDate)
                .GroupBy(a => new { a.CheckInTime.Year, a.CheckInTime.Month })
                .Select(g => new { g.Key.Year, g.Key.Month, Count = g.Count() })
                .ToListAsync();

            var monthlyAttendance = new List<MonthlyPoint>();
            for (int i = 0; i < months; i++)
            {
                var dt = startDate.AddMonths(i);
                var found = monthlyAttendanceRaw.FirstOrDefault(m => m.Year == dt.Year && m.Month == dt.Month);
                monthlyAttendance.Add(new MonthlyPoint
                {
                    Year = dt.Year,
                    Month = dt.Month,
                    Invoiced = 0m, // not used here
                    Paid = found?.Count ?? 0
                });
            }

            // Per-employee aggregates (salaries, overtimes, deductions, this-month attendance)
            var employees = await _context.Employees
                .Where(e => e.CompanyId == companyId)
                .Select(e => new EmployeeReportVM
                {
                    EmployeeId = e.Id,
                    FullName = e.FirstName + " " + e.LastName,
                    IsActive = e.IsActive,
                    TotalSalaries = _context.Salaries.Where(s => s.EmployeeId == e.Id).Sum(s => (decimal?)s.BaseAmount) ?? 0m,
                    TotalOvertimes = _context.OverTimes.Where(o => o.EmployeeId == e.Id).Sum(o => (decimal?)(o.Hours * o.Rate)) ?? 0m,
                    TotalDeductions = _context.PayrollDeductions.Where(d => d.EmployeeId == e.Id).Sum(d => (decimal?)d.Amount) ?? 0m,
                    AttendanceCountThisMonth = _context.Attendances.Count(a => a.EmployeeId == e.Id && a.CheckInTime >= startDate)
                })
                .ToListAsync();

            var absentEmployees = employees.Where(x => x.AttendanceCountThisMonth == 0).ToList();

            return new CompanyDashboardVM
            {
                CompanyId = companyId,
                CompanyName = company.Name,
                TotalEmployees = totalEmployees,
                TotalUnpaidAmount = totalUnpaidAmount,
                RecentUnpaidInvoices = recentUnpaid,
                MonthlyAttendance = monthlyAttendance,
                EmployeeReports = employees,
                AbsentEmployees = absentEmployees
            };
        }

        // Mark invoice paid for company
        public async Task<bool> PayInvoiceForCompanyAsync(int companyId, int invoiceId)
        {
            var inv = await _context.invoices.FirstOrDefaultAsync(i => i.Id == invoiceId && i.CompanyId == companyId);
            if (inv == null || inv.IsPaid) return false;
            inv.IsPaid = true;
            inv.PaidAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return true;
        }

        // Add existing identity user (by email) as an Employee to company
        public async Task<(bool Success, string Error, int EmployeeId)> AddExistingUserAsEmployeeAsync(int companyId, string userEmail, string position)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == userEmail);
            if (user == null) return (false, "User not found.", 0);

            var existing = await _context.Employees.FirstOrDefaultAsync(e => e.UserId == user.Id && e.CompanyId == companyId);
            if (existing != null) return (false, "User is already an employee of this company.", existing.Id);

            var emp = new Employee
            {
                FirstName = user.FirstName ?? "",
                LastName = user.LastName ?? "",
                Email = user.Email,
                Position = position ?? "",
                HireDate = DateTime.UtcNow,
                IsActive = true,
                UserId = user.Id,
                CompanyId = companyId
            };

            _context.Employees.Add(emp);
            await _context.SaveChangesAsync();
            return (true, null, emp.Id);
        }
    }
}
