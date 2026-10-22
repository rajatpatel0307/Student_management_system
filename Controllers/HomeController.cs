using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Student_Management_System.Models;

namespace Student_Management_System.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;

        public HomeController(ILogger<HomeController> logger)
        {
            _logger = logger;
        }

        public IActionResult Index()
        {
            return RedirectToAction(nameof(Dashboard));
        }

        // ?? Only logged-in users
        [Authorize]
        public IActionResult Dashboard()
        {
            if (User.IsInRole("SuperAdmin")) return RedirectToAction("Index", "SuperAdmin");
            if (User.IsInRole("Admin")) return RedirectToAction("Index", "Dashboard");
            if (User.IsInRole("HOD")) return RedirectToAction("Index", "HodDashboard");
            if (User.IsInRole("Professor")) return RedirectToAction("Index", "ProfessorDashboard");
            if (User.IsInRole("Student")) return RedirectToAction("Index", "StudentDashboard");
            if (User.IsInRole("Guardian")) return RedirectToAction("Index", "GuardianDashboard");
            if (User.IsInRole("Accountant")) return RedirectToAction("Finance", "RoleDashboard");
            if (User.IsInRole("Librarian")) return RedirectToAction("Library", "RoleDashboard");
            if (User.IsInRole("AdmissionOfficer")) return RedirectToAction("Admissions", "RoleDashboard");
            if (User.IsInRole("ExaminationOfficer")) return RedirectToAction("Examinations", "RoleDashboard");
            if (User.IsInRole("TransportManager") || User.IsInRole("HostelManager") ||
                User.IsInRole("PlacementOfficer") || User.IsInRole("Employee"))
                return RedirectToAction("Operations", "RoleDashboard");
            return RedirectToAction(nameof(Workspace));
        }
        [Authorize]
        public IActionResult Workspace() => View();
        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        public IActionResult AccessDenied()
        {
            return RedirectToAction("AccessDenied", "Account");
        }

        [AllowAnonymous]
        [Route("Home/StatusCode/{code:int}")]
        public IActionResult StatusCodePage(int code)
        {
            Response.StatusCode = code;
            ViewBag.StatusCode = code;
            return View("StatusCode");
        }
    }
}
