using Microsoft.AspNetCore.Mvc;
using Authenticate.Data;
using System.Threading.Tasks;
using System.Collections.Generic;
using Authenticate.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Authenticate.Controllers
{
    public class RosterController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly NFLTeamSyncService _syncService;

        public RosterController(ApplicationDbContext db, NFLTeamSyncService syncService)
        {
            _db = db;
            _syncService = syncService;
        }

        public async Task<IActionResult> Index()
        {
            List<NFLTeam> teams = await _db.NFLTeams.ToListAsync();
            ViewBag.SyncMessage = TempData["SyncMessage"];

            // Use the same methodology as _Layout: check if user is authenticated and in "Admin" role
            var isAuth = User?.Identity?.IsAuthenticated ?? false;
            var isAdmin = isAuth && User.IsInRole("Admin");
            ViewBag.IsAdmin = isAdmin;

            return View(teams);
        }

        [HttpPost]
        public async Task<IActionResult> Sync()
        {
            // Only allow sync if user is authenticated and in "Admin" role
            var isAuth = User?.Identity?.IsAuthenticated ?? false;
            var isAdmin = isAuth && User.IsInRole("Admin");
            if (!isAdmin)
                return Forbid();

            var message = await _syncService.SyncNFLTeamsAsync();
            TempData["SyncMessage"] = message;
            return RedirectToAction("Index");
        }
    }
}