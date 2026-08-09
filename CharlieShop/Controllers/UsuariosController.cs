using Microsoft.AspNetCore.Mvc;

namespace CharlieShop.Controllers
{
    public class UsuariosController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
