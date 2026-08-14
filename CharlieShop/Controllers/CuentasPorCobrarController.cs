using Microsoft.AspNetCore.Mvc;

namespace CharlieShop.Controllers
{
    public class CuentasPorCobrarController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }

        public IActionResult HistorialPagos()
        {
            return View();
        }
    }
}
