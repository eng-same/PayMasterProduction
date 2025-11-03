using Application.DAL.Data;
using Application.DAL.Models;
using Bogus;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Application.PL.Services
{
    public class FakeDataSeeder
    {
        private readonly AppDbContext _dbContext;
        private readonly UserManager<User> _userManager;

        public FakeDataSeeder(AppDbContext dbContext, UserManager<User> userManager)
        {
            _dbContext = dbContext;
            _userManager = userManager;
        }

        // Seeds two companies, each with 2 supervisors and 5 employees.
        // Generates two months of attendance with variability: absent, late, overtime.
        // Also creates overtimes, payroll deductions, salaries and invoices.
        public async Task SeedAsync()
        {
            if (await _dbContext.Companies.AnyAsync())
                return; // already seeded

            var faker = new Faker("en");

            var companies = new List<Company>();

            for (int c = 0; c < 2; c++)
            {
                var comp = new Company
                {
                    Name = faker.Company.CompanyName() + $" ({c + 1})",
                    Timezone = "UTC",
                    DefaultGraceMinutes = 5,
                    Address = faker.Address.FullAddress(),
                    PhoneNumber = faker.Phone.PhoneNumber(),
                    Email = faker.Internet.Email(),
                    BillingRatePerEmployee = faker.Random.Decimal(120, 400),
                    CreatedAt = DateTime.UtcNow
                };

                _dbContext.Companies.Add(comp);
                companies.Add(comp);
            }

            await _dbContext.SaveChangesAsync();

            foreach (var company in companies)
            {
                // create 2 supervisors
                for (int s = 0; s < 2; s++)
                {
                    var first = faker.Name.FirstName();
                    var last = faker.Name.LastName();
                    var email = $"{first}.{last}.sup{company.Id}{s}@example.com".ToLowerInvariant();

                    var user = new User
                    {
                        FirstName = first,
                        LastName = last,
                        Email = email,
                        UserName = email,
                        EmailConfirmed = true,
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow
                    };

                    var result = await _userManager.CreateAsync(user, "Supervisor@123");
                    if (result.Succeeded)
                    {
                        try { await _userManager.AddToRoleAsync(user, "Supervisor"); } catch { }
                    }

                    var sup = new CompanySupervisor
                    {
                        CompanyId = company.Id,
                        UserId = user.Id,
                        AssignedAt = DateTime.UtcNow
                    };
                    _dbContext.CompanySupervisors.Add(sup);
                }

                await _dbContext.SaveChangesAsync();

                // create 5 employees
                for (int e = 0; e < 5; e++)
                {
                    var first = faker.Name.FirstName();
                    var last = faker.Name.LastName();
                    var email = $"{first}.{last}.emp{company.Id}{e}@example.com".ToLowerInvariant();

                    var user = new User
                    {
                        FirstName = first,
                        LastName = last,
                        Email = email,
                        UserName = email,
                        EmailConfirmed = true,
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow
                    };

                    var ures = await _userManager.CreateAsync(user, "Employee@123");
                    if (ures.Succeeded)
                    {
                        try { await _userManager.AddToRoleAsync(user, "Employee"); } catch { }
                    }

                    var employee = new Employee
                    {
                        FirstName = first,
                        LastName = last,
                        Email = email,
                        Position = faker.Name.JobTitle(),
                        HireDate = faker.Date.Past(2),
                        IsActive = true,
                        CompanyId = company.Id,
                        UserId = user.Id
                    };

                    _dbContext.Employees.Add(employee);
                    await _dbContext.SaveChangesAsync();

                    // generate two full months: last two full months before now
                    var now = DateTime.UtcNow;
                    var baseMonth = new DateTime(now.Year, now.Month, 1).AddMonths(-2); // start month

                    var attendances = new List<Attendance>();
                    var overtimes = new List<OverTime>();
                    var deductions = new List<PayrollDeduction>();

                    for (int m = 0; m < 2; m++)
                    {
                        var monthStart = baseMonth.AddMonths(m);
                        var daysInMonth = DateTime.DaysInMonth(monthStart.Year, monthStart.Month);

                        for (int d = 1; d <= daysInMonth; d++)
                        {
                            var day = new DateTime(monthStart.Year, monthStart.Month, d);

                            // skip weekends
                            if (day.DayOfWeek == DayOfWeek.Saturday || day.DayOfWeek == DayOfWeek.Sunday)
                                continue;

                            // ensure employee hired before this day
                            if (day.Date < employee.HireDate.Date)
                                continue;

                            // random outcome: absent (10%), late (20%), normal (60%), early leave (10%)
                            var roll = faker.Random.Double();

                            if (roll < 0.10)
                            {
                                // Absent: no attendance entry, maybe a deduction
                                if (faker.Random.Bool(0.5f))
                                {
                                    var minutes = faker.Random.Int(30, 480);
                                    var deduction = new PayrollDeduction
                                    {
                                        EmployeeId = employee.Id,
                                        Reason = "Absent - automatic deduction",
                                        MinutesDeducted = minutes,
                                        Amount = Math.Round(faker.Random.Decimal(10, 200), 2),
                                        CreatedAt = day.AddHours(9)
                                    };
                                    deductions.Add(deduction);
                                }
                                continue;
                            }

                            // normal or late/early
                            var scheduledStart = day.AddHours(9); // 9:00
                            var scheduledEnd = day.AddHours(17); // 17:00

                            DateTime checkIn;
                            DateTime checkOut;

                            if (roll < 0.30)
                            {
                                // late: arrive between +6 and +90 minutes
                                var lateMinutes = faker.Random.Int(6, 90);
                                checkIn = scheduledStart.AddMinutes(lateMinutes);

                                // check out normally or slightly early
                                if (faker.Random.Bool(0.8f))
                                    checkOut = scheduledEnd.AddMinutes(faker.Random.Int(-20, 20));
                                else
                                    checkOut = scheduledEnd.AddMinutes(faker.Random.Int(-60, -1));

                                // deduction for late
                                var minutes = lateMinutes;
                                var deduction = new PayrollDeduction
                                {
                                    EmployeeId = employee.Id,
                                    Reason = "Late arrival",
                                    MinutesDeducted = minutes,
                                    Amount = Math.Round(minutes / 60m * faker.Random.Decimal(5, 20), 2),
                                    CreatedAt = checkIn
                                };
                                deductions.Add(deduction);
                            }
                            else
                            {
                                // on-time
                                checkIn = scheduledStart.AddMinutes(faker.Random.Int(0, 25));

                                // normal checkout
                                checkOut = scheduledEnd.AddMinutes(faker.Random.Int(-15, 60));
                            }

                            // Occasionally create overtime if checkout after scheduledEnd
                            if (checkOut > scheduledEnd && faker.Random.Bool(0.35f))
                            {
                                var otSpan = checkOut - scheduledEnd;
                                var otHours = Math.Round((decimal)otSpan.TotalHours, 2);
                                var overtime = new OverTime
                                {
                                    EmployeeId = employee.Id,
                                    Date = day,
                                    Hours = otHours,
                                    Rate = Math.Round(faker.Random.Decimal(8, 40), 2)
                                };
                                overtimes.Add(overtime);
                            }

                            var attendance = new Attendance
                            {
                                EmployeeId = employee.Id,
                                CheckInTime = checkIn,
                                CheckOutTime = checkOut,
                                Source = "Kiosk"
                            };

                            attendances.Add(attendance);
                        } // end days
                    } // end months

                    if (attendances.Any()) _dbContext.Attendances.AddRange(attendances);
                    if (overtimes.Any()) _dbContext.OverTimes.AddRange(overtimes);
                    if (deductions.Any()) _dbContext.PayrollDeductions.AddRange(deductions);

                    // create 2 salary records (one per month) - pay date is next month + 5 days
                    for (int m = 0; m < 2; m++)
                    {
                        var monthStart = baseMonth.AddMonths(m);
                        var payDate = monthStart.AddMonths(1).AddDays(5);
                        var salary = new Salary
                        {
                            EmployeeId = employee.Id,
                            BaseAmount = Math.Round(faker.Random.Decimal(1500, 4500), 2),
                            PayDate = payDate
                        };
                        _dbContext.Salaries.Add(salary);
                    }

                    await _dbContext.SaveChangesAsync();
                } // end employees

                // create invoices for the two months
                var invoiceNow = DateTime.UtcNow;
                var invoiceBase = new DateTime(invoiceNow.Year, invoiceNow.Month, 1).AddMonths(-2);
                for (int m = 0; m < 2; m++)
                {
                    var invMonth = invoiceBase.AddMonths(m);
                    var activeCount = await _dbContext.Employees.CountAsync(x => x.CompanyId == company.Id && x.IsActive);
                    var rate = company.BillingRatePerEmployee ?? 150m;
                    var invoice = new Invoice
                    {
                        CompanyId = company.Id,
                        ActiveEmployeeCount = activeCount,
                        RatePerEmployee = rate,
                        TotalAmount = Math.Round(rate * activeCount, 2),
                        Date = new DateTime(invMonth.Year, invMonth.Month, 1),
                        IsPaid = faker.Random.Bool(0.5f),
                        PaidAt = faker.Random.Bool(0.5f) ? (DateTime?)DateTime.UtcNow.AddDays(faker.Random.Int(1, 10)) : null,
                        Notes = faker.Lorem.Sentence(6)
                    };
                    _dbContext.invoices.Add(invoice);
                }

                await _dbContext.SaveChangesAsync();

            } // end companies foreach
        }
    }
}
