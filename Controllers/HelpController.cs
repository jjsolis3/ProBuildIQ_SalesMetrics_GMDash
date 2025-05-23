using Microsoft.AspNetCore.Mvc;

namespace SalesMetrics.Controllers
{
    public class HelpController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
