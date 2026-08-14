using Microsoft.AspNetCore.Mvc;

namespace CharlieShop.Controllers
{
    public class ClientesController : Controller
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