using Application.PL.ViewModels;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using Application.DAL.Data;
using Application.DAL.Models;

namespace Application.PL.Controllers
{
    public class VisitorController : Controller
    {
        private readonly ILogger<VisitorController> _logger;
        private readonly AppDbContext _db;

        public VisitorController(ILogger<VisitorController> logger, AppDbContext db)
        {
            _logger = logger;
            _db = db;
        }

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        // GET: /Visitor/Request
        [HttpGet]
        public IActionResult Request()
        {
            var vm = new VisitorRequestViewModel { NumberOfEmployees = 1 };
            return View(vm);
        }

        // POST: /Visitor/Request
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Request(VisitorRequestViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var req = new VisitorRequest
            {
                CompanyName = model.CompanyName,
                ContactName = model.ContactName,
                Email = model.Email,
                Phone = model.Phone,
                Message = model.Message,
                NumberOfEmployees = model.NumberOfEmployees,
                Password = model.Password
            };

            _db.VisitorRequests.Add(req);
            await _db.SaveChangesAsync();

            TempData["Success"] = "Your request has been submitted. We will contact you once approved.";
            return RedirectToAction(nameof(Request));
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorVM { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
