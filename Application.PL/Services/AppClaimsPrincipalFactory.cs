using Application.DAL.Data;
using Application.DAL.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Security.Claims;

namespace Application.PL.Services
{
    public class AppClaimsPrincipalFactory : UserClaimsPrincipalFactory<User, IdentityRole>
    {
        private readonly AppDbContext _db;

        public AppClaimsPrincipalFactory(
            UserManager<User> userManager,
            RoleManager<IdentityRole> roleManager,
            IOptions<IdentityOptions> options,
            AppDbContext db)
            : base(userManager, roleManager, options)
        {
            _db = db;
        }

        public override async Task<ClaimsPrincipal> CreateAsync(User user)
        {
            // Let Identity build its normal principal first (includes roles)
            var principal = await base.CreateAsync(user);
            var identity = (ClaimsIdentity)principal.Identity;

            if (await base.UserManager.IsInRoleAsync(user, "Employee"))
            {
                // Add companyId claim if employee exists
                var employee = await _db.Employees
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.UserId == user.Id);

                if (employee != null)
                {
                    identity.AddClaim(new Claim("companyId", employee.CompanyId.ToString()));
                }
            }
            // Add a friendly full name claim (optional)
            identity.AddClaim(new Claim("fullName", $"{user.FirstName} {user.LastName}"));

            return principal;
        }
    }

}
