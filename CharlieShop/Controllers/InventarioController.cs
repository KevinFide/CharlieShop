using Microsoft.AspNetCore.Mvc;

namespace CharlieShop.Controllers
{
    public class InventarioController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Historial()
        {
            return View();
        }
    }
}
