using Microsoft.AspNetCore.Mvc;

namespace CharlieShop.Controllers
{
    public class VentasController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}