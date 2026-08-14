using Microsoft.AspNetCore.Mvc;

namespace CharlieShop.Controllers
{
    public class ProductosController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Detalle()
        {
            return View();
        }
    }
}