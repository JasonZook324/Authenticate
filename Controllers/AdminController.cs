using System.Globalization;
using System.Security.Claims;
using Authenticate.Data;
using Authenticate.Data.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Authenticate.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly ILogger<AdminController> _logger;

        public AdminController(ApplicationDbContext db, ILogger<AdminController> logger)
        {
            _db = db;
            _logger = logger;
        }

        // GET /Admin/AdminDashboard
        [HttpGet]
        public async Task<IActionResult> AdminDashboard()
        {
            var menus = await _db.Menus
                .OrderBy(m => m.Weight)
                .ThenBy(m => m.Id)
                .ToListAsync();

            return View(menus);
        }

        // POST /Admin/AddMenu
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddMenu(string name, string target, int weight, bool isActive)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(target))
                {
                    TempData["Message"] = "Name and Target are required.";
                    TempData["MessageClass"] = "alert-danger";
                    return RedirectToAction(nameof(AdminDashboard));
                }

                var menu = new Menu
                {
                    Name = name.Trim(),
                    Target = target.Trim(),
                    Weight = weight,
                    IsActive = isActive
                };

                _db.Menus.Add(menu);
                await _db.SaveChangesAsync();

                TempData["Message"] = "Menu item added.";
                TempData["MessageClass"] = "alert-success";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to add menu item");
                TempData["Message"] = "An error occurred while adding the menu item.";
                TempData["MessageClass"] = "alert-danger";
            }

            return RedirectToAction(nameof(AdminDashboard));
        }

        // POST /Admin/UpdateMenu
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateMenu(int id, string name, string target, int weight, bool isActive)
        {
            try
            {
                var menu = await _db.Menus.FindAsync(id);
                if (menu == null)
                {
                    TempData["Message"] = "Menu not found.";
                    TempData["MessageClass"] = "alert-warning";
                    return RedirectToAction(nameof(AdminDashboard));
                }

                if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(target))
                {
                    TempData["Message"] = "Name and Target are required.";
                    TempData["MessageClass"] = "alert-danger";
                    return RedirectToAction(nameof(AdminDashboard));
                }

                menu.Name = name.Trim();
                menu.Target = target.Trim();
                menu.Weight = weight;
                menu.IsActive = isActive;

                await _db.SaveChangesAsync();

                TempData["Message"] = "Menu updated.";
                TempData["MessageClass"] = "alert-success";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update menu item {Id}", id);
                TempData["Message"] = "An error occurred while updating the menu item.";
                TempData["MessageClass"] = "alert-danger";
            }

            return RedirectToAction(nameof(AdminDashboard));
        }

        // POST /Admin/DeleteMenu
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteMenu(int id)
        {
            try
            {
                var menu = await _db.Menus.FindAsync(id);
                if (menu == null)
                {
                    TempData["Message"] = "Menu not found.";
                    TempData["MessageClass"] = "alert-warning";
                    return RedirectToAction(nameof(AdminDashboard));
                }

                _db.Menus.Remove(menu);
                await _db.SaveChangesAsync();

                TempData["Message"] = "Menu deleted.";
                TempData["MessageClass"] = "alert-success";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete menu item {Id}", id);
                TempData["Message"] = "An error occurred while deleting the menu item.";
                TempData["MessageClass"] = "alert-danger";
            }

            return RedirectToAction(nameof(AdminDashboard));
        }
    }
}