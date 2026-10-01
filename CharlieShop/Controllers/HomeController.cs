using CharlieShop.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace CharlieShop.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;

        public HomeController(ILogger<HomeController> logger)
        {
            _logger = logger;
        }


        // =====================================================
        // INICIO
        // =====================================================

        [Authorize]
        public IActionResult HomePage()
        {
            return View();
        }


        // =====================================================
        // DASHBOARD
        // =====================================================

        [Authorize]
        public IActionResult Dashboard()
        {
            return View();
        }


        // =====================================================
        // INDEX
        // =====================================================

        public IActionResult Index()
        {
            return View();
        }


        // =====================================================
        // PRIVACY
        // =====================================================

        public IActionResult Privacy()
        {
            return View();
        }


        // =====================================================
        // ERROR
        // =====================================================

        [ResponseCache(
            Duration = 0,
            Location = ResponseCacheLocation.None,
            NoStore = true)]
        public IActionResult Error()
        {
            return View(
                new ErrorViewModel
                {
                    RequestId =
                        Activity.Current?.Id
                        ?? HttpContext.TraceIdentifier
                });
        }
    }
}