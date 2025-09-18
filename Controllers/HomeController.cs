using System.Diagnostics;
using Authenticate.Models;
using Microsoft.AspNetCore.Mvc;
using Authenticate.Data;
using Authenticate.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Authenticate.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly ApplicationDbContext _db;

        public HomeController(ILogger<HomeController> logger, ApplicationDbContext db)
        {
            _logger = logger;
            _db = db;
        }

        public async Task<IActionResult> Index()
        {
            var menus = await _db.Menus
                .OrderBy(m => m.Weight)
                .ThenBy(m => m.Id)
                .ToListAsync();

            return View(menus);
        }

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
                    return RedirectToAction(nameof(Index));
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

            return RedirectToAction(nameof(Index));
        }

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
                    return RedirectToAction(nameof(Index));
                }

                if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(target))
                {
                    TempData["Message"] = "Name and Target are required.";
                    TempData["MessageClass"] = "alert-danger";
                    return RedirectToAction(nameof(Index));
                }

                menu.Name = name.Trim();
                menu.Target = target.Trim();
                menu.Weight = weight;
                menu.IsActive = isActive;

                await _db.SaveChangesAsync();

                TempData["Message"] = "Menu item updated.";
                TempData["MessageClass"] = "alert-success";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update menu item {Id}", id);
                TempData["Message"] = "An error occurred while updating the menu item.";
                TempData["MessageClass"] = "alert-danger";
            }

            return RedirectToAction(nameof(Index));
        }

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
                    return RedirectToAction(nameof(Index));
                }

                _db.Menus.Remove(menu);
                await _db.SaveChangesAsync();

                TempData["Message"] = "Menu item deleted.";
                TempData["MessageClass"] = "alert-success";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete menu item {Id}", id);
                TempData["Message"] = "An error occurred while deleting the menu item.";
                TempData["MessageClass"] = "alert-danger";
            }

            return RedirectToAction(nameof(Index));
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
