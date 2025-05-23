using Microsoft.AspNetCore.Mvc;

namespace SalesMetrics.Controllers
{
    public class AppsController : Controller
    {
        // GET: Apps
        public IActionResult chat()
        {
            return View();
        }
        public IActionResult contact()
        {
            return View();
        }
        public IActionResult kanban()
        {
            return View();
        }
    }
}