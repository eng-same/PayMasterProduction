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

        // --- Jobs ---

        public async Task<IEnumerable<Job>> GetJobsForCompanyAsync(int companyId)
        {
            return await _context.Jobs
                .Where(j => j.CompanyId == companyId)
                .OrderBy(j => j.Title)
                .ToListAsync();
        }

        public async Task<Job> GetJobByIdAsync(int companyId, int jobId)
        {
            return await _context.Jobs
                .Include(j => j.Company)
                .FirstOrDefaultAsync(j => j.Id == jobId && j.CompanyId == companyId);
        }

        public async Task<(bool Success, string Error, int JobId)> CreateJobAsync(Job job)
        {
            if (job == null) return (false, "Job required.", 0);
            // Basic validation
            if (string.IsNullOrWhiteSpace(job.Title)) return (false, "Title required.", 0);
            if (string.IsNullOrWhiteSpace(job.Description)) return (false, "Description required.", 0);

            // ensure required fields have defaults if missing
            if (string.IsNullOrWhiteSpace(job.Status)) job.Status = "Open";

            try
            {
                _context.Jobs.Add(job);
                await _context.SaveChangesAsync();
                return (true, null, job.Id);
            }
            catch (Microsoft.EntityFrameworkCore.DbUpdateException dbEx)
            {
                // return friendly error rather than throwing to controller
                return (false, "Database error while creating job: " + dbEx.InnerException?.Message ?? dbEx.Message, 0);
            }
            catch (Exception ex)
            {
                return (false, "Unexpected error while creating job: " + ex.Message, 0);
            }
        }

        public async Task<(bool Success, string Error)> UpdateJobAsync(int companyId, Job job)
        {
            if (job == null) return (false, "Job required.");
            var existing = await _context.Jobs.FirstOrDefaultAsync(j => j.Id == job.Id && j.CompanyId == companyId);
            if (existing == null) return (false, "Job not found.");

            // update fields
            existing.Title = job.Title;
            existing.Description = job.Description;
            existing.Salary = job.Salary;
            existing.Status = job.Status;
            existing.StandardDurationMinutes = job.StandardDurationMinutes;
            existing.StandardStartTime = job.StandardStartTime;
            existing.StandardEndTime = job.StandardEndTime;
            existing.AllowedDailyMinutes = job.AllowedDailyMinutes;

            _context.Jobs.Update(existing);
            await _context.SaveChangesAsync();
            return (true, null);
        }

        public async Task<(bool Success, string Error)> DeleteJobAsync(int companyId, int jobId)
        {
            var job = await _context.Jobs.FirstOrDefaultAsync(j => j.Id == jobId && j.CompanyId == companyId);
            if (job == null) return (false, "Job not found.");

            // Because employees reference JobId (nullable) we configured SetNull on delete in DbContext (see below).
            _context.Jobs.Remove(job);
            await _context.SaveChangesAsync();
            return (true, null);
        }

        // --- Employee / assignment ---

        public async Task<(bool Success, string Error, int EmployeeId)> AddExistingUserAsEmployeeAsync(int companyId, string userId, string position, int? jobId = null)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null) return (false, "User not found.", 0);

            var existing = await _context.Employees.FirstOrDefaultAsync(e => e.UserId == user.Id && e.CompanyId == companyId);
            if (existing != null) return (false, "User is already an employee of this company.", existing.Id);

            if (jobId.HasValue)
            {
                var job = await _context.Jobs.FirstOrDefaultAsync(j => j.Id == jobId.Value && j.CompanyId == companyId);
                if (job == null) return (false, "Job does not exist or does not belong to this company.", 0);
            }

            var emp = new Employee
            {
                FirstName = user.FirstName ?? "",
                LastName = user.LastName ?? "",
                Email = user.Email,
                Position = position ?? "",
                HireDate = DateTime.UtcNow,
                IsActive = true,
                UserId = user.Id,
                CompanyId = companyId,
                JobId = jobId
            };

            _context.Employees.Add(emp);
            await _context.SaveChangesAsync();
            return (true, null, emp.Id);
        }

        public async Task AssignJobToEmployeeAsync(int employeeId, int? jobId)
        {
            var emp = await _context.Employees.FirstOrDefaultAsync(e => e.Id == employeeId);
            if (emp == null) throw new InvalidOperationException("Employee not found.");

            if (jobId.HasValue)
            {
                var job = await _context.Jobs.FirstOrDefaultAsync(j => j.Id == jobId.Value && j.CompanyId == emp.CompanyId);
                if (job == null) throw new InvalidOperationException("Job not found for this company.");
            }

            emp.JobId = jobId;
            _context.Employees.Update(emp);
            await _context.SaveChangesAsync();
        }

        public async Task<bool> IsUserAlreadyEmployeeInCompanyAsync(string userId, int companyId)
        {
            return await _context.Employees.AnyAsync(e => e.UserId == userId && e.CompanyId == companyId);
        }

        // Add method to get employee specific dashboard
        public async Task<EmployeeDashboardVM> GetEmployeeDashboardAsync(int employeeId, int months = 6)
        {
            var now = DateTime.UtcNow;
            var startDate = new DateTime(now.Year, now.Month, 1).AddMonths(-(months - 1));
            var endDate = now;

            var emp = await _context.Employees
                .Include(e => e.Company)
                .FirstOrDefaultAsync(e => e.Id == employeeId);
            if (emp == null) return null;

            var totalSalaries = await _context.Salaries.Where(s => s.EmployeeId == employeeId).SumAsync(s => (decimal?)s.BaseAmount) ?? 0m;
            var totalOvertimes = await _context.OverTimes.Where(o => o.EmployeeId == employeeId).SumAsync(o => (decimal?)(o.Hours * o.Rate)) ?? 0m;
            var totalDeductions = await _context.PayrollDeductions.Where(d => d.EmployeeId == employeeId).SumAsync(d => (decimal?)d.Amount) ?? 0m;

            var recentSalaries = await _context.Salaries.Where(s => s.EmployeeId == employeeId)
                .OrderByDescending(s => s.PayDate).Take(6)
                .Select(s => new RecentSalaryVM { Id = s.Id, PayDate = s.PayDate, Amount = s.BaseAmount })
                .ToListAsync();

            var recentDeductions = await _context.PayrollDeductions.Where(d => d.EmployeeId == employeeId)
                .OrderByDescending(d => d.CreatedAt).Take(6)
                .Select(d => new RecentDeductionVM { Id = d.Id, CreatedAt = d.CreatedAt, Amount = d.Amount, Reason = d.Reason })
                .ToListAsync();

            var recentOvertimes = await _context.OverTimes.Where(o => o.EmployeeId == employeeId)
                .OrderByDescending(o => o.Date).Take(6)
                .Select(o => new RecentOvertimeVM { Id = o.Id, Date = o.Date, Hours = o.Hours, Rate = o.Rate })
                .ToListAsync();

            // monthly attendance counts
            var monthlyAttendanceRaw = await _context.Attendances
                .Where(a => a.EmployeeId == employeeId && a.CheckInTime >= startDate && a.CheckInTime <= endDate)
                .GroupBy(a => new { a.CheckInTime.Year, a.CheckInTime.Month })
                .Select(g => new { g.Key.Year, g.Key.Month, Count = g.Count() })
                .ToListAsync();

            var monthlyAttendance = new List<MonthlyPoint>();
            for (int i = 0; i < months; i++)
            {
                var dt = startDate.AddMonths(i);
                var found = monthlyAttendanceRaw.FirstOrDefault(m => m.Year == dt.Year && m.Month == dt.Month);
                monthlyAttendance.Add(new MonthlyPoint { Year = dt.Year, Month = dt.Month, Invoiced = 0m, Paid = found?.Count ?? 0 });
            }

            // recent attendance days for calendar (last 90 days)
            var fromDay = now.AddDays(-90);
            var attendanceDays = await _context.Attendances
                .Where(a => a.EmployeeId == employeeId && a.CheckInTime >= fromDay)
                .Select(a => new AttendanceDayVM { Date = a.CheckInTime.Date, Status = "present" })
                .ToListAsync();

            // deduce absent days (simple approach): we won't synthesize absents here

            return new EmployeeDashboardVM
            {
                EmployeeId = emp.Id,
                FullName = emp.FirstName + " " + emp.LastName,
                Position = emp.Position,
                CompanyId = emp.CompanyId,
                CompanyName = emp.Company?.Name,
                CompanyAddress = emp.Company?.Address,
                TotalSalaries = totalSalaries,
                TotalOvertimes = totalOvertimes,
                TotalDeductions = totalDeductions,
                RecentSalaries = recentSalaries,
                RecentDeductions = recentDeductions,
                RecentOvertimes = recentOvertimes,
                MonthlyAttendance = monthlyAttendance,
                RecentAttendanceDays = attendanceDays
            };
        }

        // New helper: return attendance events for calendar as JSON-friendly objects
        public async Task<List<object>> GetAttendanceEventsAsync(int employeeId, int days = 90)
        {
            var since = DateTime.UtcNow.Date.AddDays(-days + 1);
            var until = DateTime.UtcNow.Date;

            var emp = await _context.Employees.Include(e => e.Job).Include(e => e.Company).FirstOrDefaultAsync(e => e.Id == employeeId);
            if (emp == null) return new List<object>();

            // load attendances in range
            var atts = await _context.Attendances
                .Where(a => a.EmployeeId == employeeId && a.CheckInTime.Date >= since && a.CheckInTime.Date <= until)
                .ToListAsync();

            // load payroll deductions in range and map by date
            var deductions = await _context.PayrollDeductions
                .Where(d => d.EmployeeId == employeeId && d.CreatedAt.Date >= since && d.CreatedAt.Date <= until)
                .ToListAsync();

            var deductionsByDate = deductions
                .GroupBy(d => d.CreatedAt.Date)
                .ToDictionary(g => g.Key, g => g.ToList());

            var events = new List<object>();

            // determine grace minutes
            int grace = emp.Company?.DefaultGraceMinutes ?? 0;

            // job standard times
            TimeSpan? stdStart = emp.Job?.StandardStartTime;
            TimeSpan? stdEnd = emp.Job?.StandardEndTime;

            // map by date
            var attByDate = atts.GroupBy(a => a.CheckInTime.Date).ToDictionary(g => g.Key, g => g.ToList());

            for (var date = since; date <= until; date = date.AddDays(1))
            {
                if (attByDate.TryGetValue(date, out var list))
                {
                    // pick earliest checkin and latest checkout
                    var earliest = list.OrderBy(a => a.CheckInTime).First();
                    var latest = list.Where(a => a.CheckOutTime.HasValue).OrderByDescending(a => a.CheckOutTime).FirstOrDefault();

                    // aggregate deductions for the day if any
                    int minutesDeducted = 0;
                    string deductionReasons = null;
                    if (deductionsByDate.TryGetValue(date, out var dedList))
                    {
                        minutesDeducted = dedList.Sum(d => d.MinutesDeducted);
                        deductionReasons = string.Join("; ", dedList.Select(d => d.Reason).Where(r => !string.IsNullOrEmpty(r)));
                    }

                    // determine status
                    string status = "present";
                    string color = "#4caf50"; // green

                    if (stdStart.HasValue)
                    {
                        var scheduled = date + stdStart.Value;
                        var allowed = scheduled.AddMinutes(grace);
                        if (earliest.CheckInTime > allowed)
                        {
                            status = "late";
                            color = "#f44336"; // red
                        }
                    }

                    double overtimeHours = 0;
                    if (stdEnd.HasValue && latest?.CheckOutTime != null)
                    {
                        var scheduledEnd = date + stdEnd.Value;
                        // if checkout earlier than scheduled end minus grace -> early leave
                        var threshold = scheduledEnd.AddMinutes(-grace);
                        if (latest.CheckOutTime.Value < threshold)
                        {
                            status = "early";
                            color = "#f44336"; // red
                        }
                        else
                        {
                            // checkout later than scheduled end -> overtime
                            if (latest.CheckOutTime.Value > scheduledEnd)
                            {
                                var overtimeSpan = latest.CheckOutTime.Value - scheduledEnd;
                                overtimeHours = Math.Round(overtimeSpan.TotalHours, 2);

                                // create distinct overtime event with extendedProps
                                events.Add(new
                                {
                                    id = $"ot_{latest.Id}",
                                    title = $"Overtime {overtimeHours} hrs",
                                    start = scheduledEnd,
                                    end = latest.CheckOutTime.Value,
                                    color = "#1976d2",
                                    backgroundColor = "#1976d2",
                                    allDay = false,
                                    extendedProps = new {
                                        overtimeHours = overtimeHours,
                                        minutesDeducted = minutesDeducted,
                                        deductionReasons = deductionReasons
                                    }
                                });
                            }
                        }
                    }

                    // create event for the check-in time (status event) with extendedProps
                    events.Add(new
                    {
                        id = earliest.Id,
                        title = status == "present" ? "Present" : (status == "late" ? "Late" : "Early checkout"),
                        start = earliest.CheckInTime,
                        end = latest?.CheckOutTime,
                        color = color,
                        backgroundColor = color,
                        allDay = false,
                        extendedProps = new {
                            status = status,
                            overtimeHours = overtimeHours,
                            minutesDeducted = minutesDeducted,
                            deductionReasons = deductionReasons
                        }
                    });
                }
                else
                {
                    // absent day - mark only weekdays and past hire date
                    if (date >= emp.HireDate.Date && date <= DateTime.UtcNow.Date && date.DayOfWeek != DayOfWeek.Saturday && date.DayOfWeek != DayOfWeek.Sunday)
                    {
                        // attach any deductions recorded for that absent date
                        int minutesDeducted = 0;
                        string deductionReasons = null;
                        if (deductionsByDate.TryGetValue(date, out var dedList))
                        {
                            minutesDeducted = dedList.Sum(d => d.MinutesDeducted);
                            deductionReasons = string.Join("; ", dedList.Select(d => d.Reason).Where(r => !string.IsNullOrEmpty(r)));
                        }

                        events.Add(new
                        {
                            id = $"absent_{date:yyyyMMdd}",
                            title = "Absent",
                            start = date,
                            allDay = true,
                            color = "#f44336",
                            backgroundColor = "#f44336",
                            extendedProps = new {
                                status = "absent",
                                minutesDeducted = minutesDeducted,
                                deductionReasons = deductionReasons
                            }
                        });
                    }
                }
            }

            return events.Cast<object>().ToList();
        }

        // New: fetch salary by id
        public async Task<Salary?> GetSalaryByIdAsync(int salaryId)
        {
            return await _context.Salaries.Include(s => s.Employee).FirstOrDefaultAsync(s => s.Id == salaryId);
        }

        // New: fetch deduction by id
        public async Task<PayrollDeduction?> GetDeductionByIdAsync(int deductionId)
        {
            return await _context.PayrollDeductions.Include(d => d.Employee).FirstOrDefaultAsync(d => d.Id == deductionId);
        }

        // New: get all deductions for an employee
        public async Task<List<PayrollDeduction>> GetDeductionsForEmployeeAsync(int employeeId)
        {
            return await _context.PayrollDeductions.Where(d => d.EmployeeId == employeeId).OrderByDescending(d => d.CreatedAt).ToListAsync();
        }

        // --- Employee helpers (added) ---
        public async Task<List<Employee>> GetEmployeesForCompanyAsync(int companyId)
        {
            return await _context.Employees
                .Where(e => e.CompanyId == companyId)
                .OrderBy(e => e.FirstName).ThenBy(e => e.LastName)
                .ToListAsync();
        }

        public async Task<Employee?> GetEmployeeByIdAsync(int companyId, int employeeId)
        {
            return await _context.Employees.FirstOrDefaultAsync(e => e.CompanyId == companyId && e.Id == employeeId);
        }

        public async Task<(bool Success, string Error, int EmployeeId)> CreateEmployeeAsync(Employee emp)
        {
            if (emp == null) return (false, "Employee required.", 0);
            if (string.IsNullOrWhiteSpace(emp.FirstName)) return (false, "First name required.", 0);

            _context.Employees.Add(emp);
            await _context.SaveChangesAsync();
            return (true, null, emp.Id);
        }

        public async Task<(bool Success, string Error)> UpdateEmployeeAsync(int companyId, Employee emp)
        {
            if (emp == null) return (false, "Employee required.");
            var existing = await _context.Employees.FirstOrDefaultAsync(e => e.Id == emp.Id && e.CompanyId == companyId);
            if (existing == null) return (false, "Employee not found.");

            existing.FirstName = emp.FirstName;
            existing.LastName = emp.LastName;
            existing.Email = emp.Email;
            existing.Position = emp.Position;
            existing.IsActive = emp.IsActive;
            existing.JobId = emp.JobId;

            _context.Employees.Update(existing);
            await _context.SaveChangesAsync();
            return (true, null);
        }

        public async Task<(bool Success, string Error)> DeleteEmployeeAsync(int companyId, int employeeId)
        {
            var emp = await _context.Employees.FirstOrDefaultAsync(e => e.Id == employeeId && e.CompanyId == companyId);
            if (emp == null) return (false, "Employee not found.");

            // soft-delete may be preferred; here we remove
            _context.Employees.Remove(emp);
            await _context.SaveChangesAsync();
            return (true, null);
        }

        // --- Leave request helpers (added) ---
        public async Task<List<LeaveRequest>> GetLeaveRequestsForCompanyAsync(int companyId)
        {
            return await _context.LeaveRequests
                .Include(l => l.Employee)
                .Where(l => l.Employee != null && l.Employee.CompanyId == companyId)
                .OrderByDescending(l => l.SubmittedAt)
                .ToListAsync();
        }

        public async Task<(bool Success, string Error)> ApproveLeaveRequestAsync(int companyId, int leaveRequestId, string approvedByUserId)
        {
            var lr = await _context.LeaveRequests.Include(l => l.Employee).FirstOrDefaultAsync(l => l.Id == leaveRequestId);
            if (lr == null) return (false, "Leave request not found.");
            if (lr.Employee == null || lr.Employee.CompanyId != companyId) return (false, "Leave request does not belong to your company.");

            lr.Status = "Approved";
            lr.SubmittedAt = lr.SubmittedAt; // keep
            await _context.SaveChangesAsync();

            // In production you'd create a notification record or send email - omitted per request
            return (true, null);
        }

        public async Task<(bool Success, string Error)> RejectLeaveRequestAsync(int companyId, int leaveRequestId, string rejectedByUserId)
        {
            var lr = await _context.LeaveRequests.Include(l => l.Employee).FirstOrDefaultAsync(l => l.Id == leaveRequestId);
            if (lr == null) return (false, "Leave request not found.");
            if (lr.Employee == null || lr.Employee.CompanyId != companyId) return (false, "Leave request does not belong to your company.");

            lr.Status = "Rejected";
            await _context.SaveChangesAsync();

            // notification/email omitted
            return (true, null);
        }

        // --- Payroll processing stub (added) ---
        public async Task<(bool Success, string Error)> RunPayrollForCompanyAsync(int companyId)
        {
            // This is intentionally a stub. Actual payroll processing is complex and omitted.
            await Task.Delay(200); // simulate work
            return (true, null);
        }

        // Map identity user id to employee record
        public async Task<Employee?> GetEmployeeByUserIdAsync(string userId)
        {
            if (string.IsNullOrEmpty(userId)) return null;
            return await _context.Employees.Include(e => e.Company).Include(e => e.Job).FirstOrDefaultAsync(e => e.UserId == userId);
        }

        // Compute salary breakdown for a salary record: aggregate overtime & deductions for the salary period
        public async Task<SalaryBreakdownVM?> GetSalaryBreakdownAsync(int salaryId)
        {
            var salary = await _context.Salaries.Include(s => s.Employee).FirstOrDefaultAsync(s => s.Id == salaryId);
            if (salary == null) return null;

            // For simplicity, treat salary.BaseAmount as monthly for the month of PayDate
            var periodStart = new DateTime(salary.PayDate.Year, salary.PayDate.Month, 1).AddMonths(-1); // salary often paid next month: we take previous month
            var periodEnd = periodStart.AddMonths(1).AddDays(-1);

            // sum overtime entries for employee that fall into period
            var overtimes = await _context.OverTimes
                .Where(o => o.EmployeeId == salary.EmployeeId && o.Date >= periodStart && o.Date <= periodEnd)
                .ToListAsync();

            var deductions = await _context.PayrollDeductions
                .Where(d => d.EmployeeId == salary.EmployeeId && d.CreatedAt.Date >= periodStart.Date && d.CreatedAt.Date <= periodEnd.Date)
                .ToListAsync();

            var totalOvertime = overtimes.Sum(o => (decimal?)(o.Hours * o.Rate)) ?? 0m;
            var totalDeductions = deductions.Sum(d => (decimal?)d.Amount) ?? 0m;

            var vm = new SalaryBreakdownVM
            {
                SalaryId = salary.Id,
                BaseAmount = salary.BaseAmount,
                TotalOvertime = totalOvertime,
                TotalDeductions = totalDeductions,
                NetPay = salary.BaseAmount + totalOvertime - totalDeductions,
                PeriodStart = periodStart,
                PeriodEnd = periodEnd,
                Overtimes = overtimes.Select(o => new RecentOvertimeVM { Id = o.Id, Date = o.Date, Hours = o.Hours, Rate = o.Rate }).ToList(),
                Deductions = deductions.Select(d => new RecentDeductionVM { Id = d.Id, CreatedAt = d.CreatedAt, Amount = d.Amount, Reason = d.Reason }).ToList()
            };

            return vm;
        }
    }
}
