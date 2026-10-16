using KYCSACCO.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace KYCSACCO.Controllers
{
    [Authorize]
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;

        public HomeController(ILogger<HomeController> logger)
        {
            _logger = logger;
        }

        [Authorize]
        public IActionResult Index()
        {
            // ---------- KPI CARDS (Static) ----------
            ViewBag.TotalCompanies = 128;
            ViewBag.TotalMembers = 5420;
            ViewBag.TotalSavings = 1_250_000.00m;
            ViewBag.TotalLoans = 780_500.00m;
            ViewBag.ActiveUsers = 312;
            ViewBag.PendingApprovals = 17;

            // ---------- PIE CHART: Company Distribution by Sector ----------
            ViewBag.PieLabels = new List<string> { "SACCO", "Bank", "Microfinance", "Insurance", "Fintech" };
            ViewBag.PieData = new List<int> { 45, 25, 15, 10, 5 };

            // ---------- LINE GRAPH: Monthly New Members ----------
            ViewBag.LineLabels = new List<string>
                { "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec" };
            ViewBag.LineData = new List<int>
                { 120, 190, 300, 250, 420, 380, 510, 470, 620, 580, 700, 750 };

            // ---------- BAR CHART: Loans Disbursed vs Repaid ----------
            ViewBag.BarLabels = new List<string> { "Q1", "Q2", "Q3", "Q4" };
            ViewBag.BarDisbursed = new List<decimal> { 150000m, 220000m, 180000m, 260000m };
            ViewBag.BarRepaid = new List<decimal> { 130000m, 200000m, 160000m, 240000m };

            // ---------- DOUGHNUT / SECOND PIE: User Roles ----------
            ViewBag.RoleLabels = new List<string> { "Admin", "Manager", "Staff", "Auditor", "Viewer" };
            ViewBag.RoleData = new List<int> { 8, 22, 120, 15, 147 };

            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}