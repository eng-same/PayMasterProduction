using Microsoft.AspNetCore.Mvc;

namespace Application.PL.Areas.Supervisor.Controllers
{
    [Area("Supervisor")]
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
