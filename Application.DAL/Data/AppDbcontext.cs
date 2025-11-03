using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Application.DAL.Models;
namespace Application.DAL.Data
{
    public class AppDbContext : IdentityDbContext<User>
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        // DbSets
        public DbSet<Company> Companies { get; set; }
        public DbSet<Employee> Employees { get; set; }
        public DbSet<CompanySupervisor> CompanySupervisors { get; set; }
        public DbSet<Job> Jobs { get; set; }
        public DbSet<Device> Devices { get; set; }
        public DbSet<CompanyQRCode> CompanyQRCodes { get; set; }
        public DbSet<Attendance> Attendances { get; set; }
        public DbSet<OverTime> OverTimes { get; set; }
        public DbSet<PayrollDeduction> PayrollDeductions { get; set; }
        public DbSet<Salary> Salaries { get; set; }
        public DbSet<LeaveRequest> LeaveRequests { get; set; }
        public DbSet<Invoice> invoices { get; set; }
        public DbSet<VisitorRequest> VisitorRequests { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // -----------------------
            // CompanySupervisor
            // -----------------------

            builder.Entity<CompanySupervisor>()
                .HasOne(x => x.Company).WithMany(c => c.Supervisors).HasForeignKey(x => x.CompanyId);
            
            builder.Entity<CompanySupervisor>()
                .HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId);



            // -----------------------
            // Company
            // -----------------------
            builder.Entity<Company>(b =>
            {
                b.HasKey(c => c.Id);
                b.HasIndex(c => c.Name);
                b.Property(c => c.Name).IsRequired().HasMaxLength(200);
                b.Property(c => c.Timezone).HasMaxLength(100).IsRequired();
                b.Property(c => c.DefaultGraceMinutes).HasDefaultValue(0);
                b.Property(c => c.BillingRatePerEmployee).HasColumnType("decimal(18,2)");
            });

            // -----------------------
            // Employee
            // -----------------------
            // NOTE: changed JobId and UserId handling to support nullable FK when we want SetNull on delete.
            builder.Entity<Employee>(b =>
            {
                b.HasKey(x => x.Id);

                b.HasIndex(x => x.EmployeeNo); // not unique by default

                b.Property(x => x.FirstName).IsRequired().HasMaxLength(100);
                b.Property(x => x.LastName).IsRequired().HasMaxLength(100);
                b.Property(x => x.Email).HasMaxLength(200);
                b.Property(x => x.Position).HasMaxLength(150);

                // Company (required)
                b.HasOne(e => e.Company)
                 .WithMany(c => c.Employees)
                 .HasForeignKey(e => e.CompanyId)
                 .OnDelete(DeleteBehavior.Restrict);

                // Job (optional). If you want Job required, change JobId to non-nullable and change DeleteBehavior accordingly.
                b.HasOne(e => e.Job)
                 .WithMany()
                 .HasForeignKey(e => e.JobId)
                 .OnDelete(DeleteBehavior.SetNull);

                // Link to Identity User (optional one-to-one)
                // Employee.UserId is optional; when user deleted, set Employee.UserId = null
                b.HasOne(e => e.User)
                 .WithOne() // no navigation property on User side
                 .HasForeignKey<Employee>(e => e.UserId)
                 .OnDelete(DeleteBehavior.SetNull);

                b.HasIndex(e => e.CompanyId);
            });

            // -----------------------
            // Job
            // -----------------------
            builder.Entity<Job>(b =>
            {
                b.HasKey(x => x.Id);

                b.Property(j => j.Title).IsRequired().HasMaxLength(200);
                b.Property(j => j.Description).HasMaxLength(2000);
                // precision for salary
                b.Property(j => j.Salary).HasColumnType("decimal(18,2)");

                b.HasOne(j => j.Company)
                 .WithMany(c => c.Jobs)
                 .HasForeignKey(j => j.CompanyId)
                 .OnDelete(DeleteBehavior.Cascade);
            });

            // -----------------------
            // Device
            // -----------------------
            builder.Entity<Device>(b =>
            {
                b.HasKey(x => x.Id);
                b.Property(d => d.Name).HasMaxLength(200);
                b.Property(d => d.SerialNumber).HasMaxLength(200);
                b.Property(d => d.DeviceType).HasMaxLength(100);

                b.HasOne(d => d.Company)
                 .WithMany(c => c.Devices)
                 .HasForeignKey(d => d.CompanyId)
                 .OnDelete(DeleteBehavior.Cascade);
            });

            // -----------------------
            // CompanyQRCode
            // -----------------------
            builder.Entity<CompanyQRCode>(b =>
            {
                b.HasKey(x => x.Id);
                b.Property(q => q.QRCodeToken).IsRequired().HasMaxLength(500);
                b.Property(q => q.GeneratedAt).HasDefaultValueSql("GETUTCDATE()");
                b.Property(q => q.IsActive).HasDefaultValue(true);

                b.HasOne(q => q.Company)
                 .WithMany(c => c.CompanyQRCodes)
                 .HasForeignKey(q => q.CompanyId)
                 .OnDelete(DeleteBehavior.Cascade);

                // Indexes: unique filtered index so a company can have only one active QR (SQL Server only)
                // If not using SQL Server, remove HasFilter or handle at application level.
                b.HasIndex(q => new { q.CompanyId }).HasFilter("[IsActive] = 1").IsUnique();
                b.HasIndex(q => new { q.CompanyId, q.IsActive });
            });

            // -----------------------
            // Attendance
            // -----------------------
            builder.Entity<Attendance>(b =>
            {
                b.HasKey(x => x.Id);
                b.Property(a => a.Source).HasMaxLength(100);

                b.HasOne(a => a.Employee)
                 .WithMany(e => e.Attendances)
                 .HasForeignKey(a => a.EmployeeId)
                 .OnDelete(DeleteBehavior.Restrict);
            });

            // -----------------------
            // PayrollDeduction
            // -----------------------
            builder.Entity<PayrollDeduction>(b =>
            {
                b.HasKey(x => x.Id);

                b.Property(p => p.Reason).HasMaxLength(500);
                b.Property(p => p.Amount).HasColumnType("decimal(18,2)");
                b.Property(p => p.CreatedAt).HasDefaultValueSql("GETUTCDATE()");

                b.HasOne(p => p.Employee)
                 .WithMany(e => e.PayrollDeductions)
                 .HasForeignKey(p => p.EmployeeId)
                 .OnDelete(DeleteBehavior.Restrict);

                b.HasIndex(p => p.CreatedAt);
            });

            // -----------------------
            // Salary
            // -----------------------
            builder.Entity<Salary>(b =>
            {
                b.HasKey(x => x.Id);

                b.Property(s => s.BaseAmount).HasColumnType("decimal(18,2)");
                b.Property(s => s.PayDate).IsRequired();

                b.HasOne(s => s.Employee)
                 .WithMany(e => e.Salaries)
                 .HasForeignKey(s => s.EmployeeId)
                 .OnDelete(DeleteBehavior.Cascade);
            });

            // -----------------------
            // OverTime
            // -----------------------
            builder.Entity<OverTime>(b =>
            {
                b.HasKey(x => x.Id);

                b.Property(o => o.Hours).HasColumnType("decimal(10,2)");
                b.Property(o => o.Rate).HasColumnType("decimal(18,2)");
                b.Property(o => o.Date).IsRequired();

                b.HasOne(o => o.Employee)
                .WithMany(e => e.OverTimes)
                .HasForeignKey(o => o.EmployeeId)
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();
            });

            // -----------------------
            // LeaveRequest
            // -----------------------
            builder.Entity<LeaveRequest>(b =>
            {
                b.HasKey(x => x.Id);

                b.Property(l => l.LeaveType).HasMaxLength(100);
                b.Property(l => l.Reason).HasMaxLength(1000);
                b.Property(l => l.Status).HasMaxLength(50).HasDefaultValue("Pending");
                b.Property(l => l.SubmittedAt).HasDefaultValueSql("GETUTCDATE()");

                b.HasOne(l => l.Employee)
                 .WithMany(e => e.LeaveRequests)
                 .HasForeignKey(l => l.EmployeeId)
                 .OnDelete(DeleteBehavior.SetNull); // EmployeeId is nullable in model

                b.HasIndex(l => l.Status);
            });

            // -----------------------
            // VisitorRequest
            // -----------------------
            builder.Entity<VisitorRequest>(b =>
            {
                b.HasKey(x => x.Id);
                b.Property(x => x.CompanyName).IsRequired().HasMaxLength(250);
                b.Property(x => x.ContactName).IsRequired().HasMaxLength(200);
                b.Property(x => x.Email).IsRequired().HasMaxLength(200);
                b.Property(x => x.Phone).HasMaxLength(50);
                b.Property(x => x.Message).HasMaxLength(2000);
                b.Property(x => x.NumberOfEmployees).HasDefaultValue(1);
                b.Property(x => x.Password).HasMaxLength(2000);
                b.Property(x => x.Status).HasMaxLength(50).HasDefaultValue("Pending");
                b.Property(x => x.SubmittedAt).HasDefaultValueSql("GETUTCDATE()");
                b.HasIndex(x => x.Status);
            });

            // -----------------------
            // Billing
            // -----------------------
            builder.Entity<Invoice>(b =>
            {
                b.HasKey(x => x.Id);

                b.Property(x => x.RatePerEmployee).HasColumnType("decimal(18,2)").IsRequired();
                b.Property(x => x.TotalAmount).HasColumnType("decimal(18,2)").IsRequired();
                b.Property(x => x.Notes).HasMaxLength(2000);

                b.HasOne(x => x.Company)
                 .WithMany(x => x.Invoices) 
                 .HasForeignKey(x => x.CompanyId)
                 .OnDelete(DeleteBehavior.Cascade);
                
                // Indexes to support common queries
                b.HasIndex(x => x.IsPaid);
            });

            // -----------------------
            // Other indexes
            // -----------------------
            builder.Entity<Employee>().HasIndex(e => e.CompanyId);
            builder.Entity<CompanyQRCode>().HasIndex(q => q.IsActive);
        }
    }
}