using Application.DAL.Data;
using Application.DAL.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Application.PL.Services
{
    // we have to call this class in Program.cs to seed roles and admin user
    public class IdentitySeeder
    {
        private readonly IFileService _fileService;
        private readonly AppDbContext _dbContext;
        private readonly UserManager<User> _userManager;

        public IdentitySeeder(IFileService fileService, AppDbContext dbContext, UserManager<User> userManager)
        {
            _fileService = fileService; // keep reference in case profile pic logic added later
            _dbContext = dbContext;
            _userManager = userManager;
        }

        // seed roles and a few core identity users
        public async Task SeedIdentityAsync(WebApplication app)
        {
            using var scope = app.Services.CreateScope();
            var services = scope.ServiceProvider;
            var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = services.GetRequiredService<UserManager<User>>();

            // Roles
            string[] roles = { "Admin", "Employee", "Supervisor" };
            foreach (var role in roles)
            {
                if (!await roleManager.RoleExistsAsync(role))
                    await roleManager.CreateAsync(new IdentityRole(role));
            }

            // Admin user
            string adminEmail = "admin@PayMaster.com";
            string adminPassword = "Admin@123";

            if (await userManager.FindByEmailAsync(adminEmail) == null)
            {
                var adminUser = new User
                {
                    UserName = adminEmail,
                    Email = adminEmail,
                    FirstName = "System",
                    LastName = "Admin",
                    EmailConfirmed = true
                };

                var result = await userManager.CreateAsync(adminUser, adminPassword);
                if (result.Succeeded)
                    await userManager.AddToRoleAsync(adminUser, "Admin");
            }

            // Employee user
            string employeeEmail = "employee@PayMaster.com";
            string employeePassword = "Employee@123";

            if (await userManager.FindByEmailAsync(employeeEmail) == null)
            {
                var employeeUser = new User
                {
                    UserName = employeeEmail,
                    Email = employeeEmail,
                    FirstName = "default",
                    LastName = "employee",
                    EmailConfirmed = true
                };

                var result = await userManager.CreateAsync(employeeUser, employeePassword);
                if (result.Succeeded)
                    await userManager.AddToRoleAsync(employeeUser, "Employee");
            }

            // Supervisor user
            string supervisorEmail = "supervisor@PayMaster.com";
            string supervisorPassword = "Supervisor@123";

            if (await userManager.FindByEmailAsync(supervisorEmail) == null)
            {
                var supervisorUser = new User
                {
                    UserName = supervisorEmail,
                    Email = supervisorEmail,
                    FirstName = "default",
                    LastName = "supervisor",
                    EmailConfirmed = true
                };

                var result = await userManager.CreateAsync(supervisorUser, supervisorPassword);
                if (result.Succeeded)
                    await userManager.AddToRoleAsync(supervisorUser, "Supervisor");
            }
        }
    }
}
