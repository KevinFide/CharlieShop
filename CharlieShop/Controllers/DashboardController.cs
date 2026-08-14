using Microsoft.AspNetCore.Mvc;

namespace CharlieShop.Controllers
{
    public class DashboardController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
