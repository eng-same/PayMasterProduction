using Application.BLL.Repositories;
using Application.BLL.Servicies;
using Application.DAL.Models;
using Application.PL.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Application.DAL.Data;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();
builder.Services.AddScoped<IFileService, FileService>();
builder.Services.AddScoped<IdentitySeeder>();
builder.Services.AddScoped<EmployeeRepository>();
builder.Services.AddScoped<EmployeeService>();
builder.Services.AddScoped<LeaveRequestRepository>();
builder.Services.AddScoped<ReportRepository>();
builder.Services.AddMemoryCache();
builder.Services.AddScoped<IUserClaimsPrincipalFactory<User>, AppClaimsPrincipalFactory>();


// register LiveQrService as singleton
// 3. Read HMAC key from configuration
var keyBase64 = builder.Configuration["Qr:HmacKey"];
if (string.IsNullOrEmpty(keyBase64))
    throw new InvalidOperationException("Missing configuration: Qr:HmacKey");

var hmacKey = Convert.FromBase64String(keyBase64);

// 4. Register LiveQrService (singleton)
builder.Services.AddSingleton(sp =>
{
    var cache = sp.GetRequiredService<IMemoryCache>();
    return new LiveQrService(hmacKey, cache);
});


builder.Services.AddScoped<QrCodeService>();

//Db
builder.Services.AddDbContext<AppDbContext>(options =>
options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
//identity
builder.Services.AddIdentity<User, IdentityRole>(options =>
{
    options.User.RequireUniqueEmail = true;
})
.AddEntityFrameworkStores<AppDbContext>()
.AddDefaultTokenProviders();

builder.Services.AddAuthorization();
var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// Add authentication middleware so Identity works
app.UseAuthentication();

app.UseAuthorization();

// Map Razor Pages so asp-page links work
app.MapRazorPages();

app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Visitor}/{action=Index}/{id?}");

// seed data
using (var scope = app.Services.CreateScope())
{
    var seeder = scope.ServiceProvider.GetRequiredService<IdentitySeeder>();

    // Seed roles and admin user
    await seeder.SeedIdentityAsync(app);

}

app.Run();
