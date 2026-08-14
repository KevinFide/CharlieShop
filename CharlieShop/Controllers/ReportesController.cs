using Microsoft.AspNetCore.Mvc;

namespace CharlieShop.Controllers
{
    public class ReportesController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Inventario()
        {
            return View();
        }

        public IActionResult Clientes()
        {
            return View();
        }
    }
}
