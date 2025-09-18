using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Authenticate.Controllers
{
    [Authorize]
    public class DashboardController : Controller
    {
        private readonly ILogger<DashboardController> _logger;

        public DashboardController(ILogger<DashboardController> logger)
        {
            _logger = logger;
        }

        // GET /Dashboard
        [HttpGet]
        public IActionResult Index()
        {
            if (User.IsInRole("Admin"))
                return RedirectToAction("AdminDashboard", "Admin");

            return View("Dashboard");
        }
    }
}