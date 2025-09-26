using System.Globalization;
using System.Security.Claims;
using Authenticate.Data;
using Authenticate.Data.Entities;
using Authenticate.Models;
using Authenticate.Infrastructure.ApiDocs;
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
        private readonly IApiDocIngestionService _ingest; // NEW

        public AdminController(ApplicationDbContext db, ILogger<AdminController> logger, IApiDocIngestionService ingest)
        {
            _db = db;
            _logger = logger;
            _ingest = ingest;
        }

        // GET /Admin/AdminDashboard
        [HttpGet]
        public async Task<IActionResult> AdminDashboard()
        {
            var vm = new AdminDashboardViewModel
            {
                Menus = await _db.Menus
                    .OrderBy(m => m.Weight).ThenBy(m => m.Id)
                    .ToListAsync(),
                ApiTypes = await _db.APITypes
                    .OrderBy(a => a.ApiName).ThenBy(a => a.Id)
                    .ToListAsync()
            };
            return View(vm);
        }

        // Menu actions
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

                _db.Menus.Add(new Menu
                {
                    Name = name.Trim(),
                    Target = target.Trim(),
                    Weight = weight,
                    IsActive = isActive
                });
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

        // ApiTypes actions
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddApiType(string apiName, string? description, string? documentationUrl)
        {
            try
            {
                apiName = (apiName ?? "").Trim();
                if (string.IsNullOrWhiteSpace(apiName))
                {
                    TempData["Message"] = "API Name is required.";
                    TempData["MessageClass"] = "alert-danger";
                    return RedirectToAction(nameof(AdminDashboard));
                }

                var exists = await _db.APITypes.AnyAsync(a => a.ApiName.ToLower() == apiName.ToLower());
                if (exists)
                {
                    TempData["Message"] = "That API Name already exists.";
                    TempData["MessageClass"] = "alert-warning";
                    return RedirectToAction(nameof(AdminDashboard));
                }

                var entity = new APITypes
                {
                    ApiName = apiName,
                    Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
                    DocumentationUrl = string.IsNullOrWhiteSpace(documentationUrl) ? null : documentationUrl.Trim()
                };

                _db.APITypes.Add(entity);
                await _db.SaveChangesAsync();

                // Trigger ingestion if doc URL provided
                if (!string.IsNullOrWhiteSpace(entity.DocumentationUrl))
                {
                    try
                    {
                        var count = await _ingest.IngestAsync(entity.Id, entity.DocumentationUrl);
                        TempData["Message"] = $"API Type added. Imported {count} endpoints.";
                        TempData["MessageClass"] = "alert-success";
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to ingest docs for API Type {Id}", entity.Id);
                        TempData["Message"] = "API Type added, but importing endpoints failed. You can retry with Refresh.";
                        TempData["MessageClass"] = "alert-warning";
                    }
                }
                else
                {
                    TempData["Message"] = "API Type added.";
                    TempData["MessageClass"] = "alert-success";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to add API Type");
                TempData["Message"] = "An error occurred while adding the API Type.";
                TempData["MessageClass"] = "alert-danger";
            }

            return RedirectToAction(nameof(AdminDashboard));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateApiType(int id, string apiName, string? description, string? documentationUrl)
        {
            try
            {
                var entity = await _db.APITypes.FindAsync(id);
                if (entity == null)
                {
                    TempData["Message"] = "API Type not found.";
                    TempData["MessageClass"] = "alert-warning";
                    return RedirectToAction(nameof(AdminDashboard));
                }

                apiName = (apiName ?? "").Trim();
                if (string.IsNullOrWhiteSpace(apiName))
                {
                    TempData["Message"] = "API Name is required.";
                    TempData["MessageClass"] = "alert-danger";
                    return RedirectToAction(nameof(AdminDashboard));
                }

                var duplicate = await _db.APITypes.AnyAsync(a => a.Id != id && a.ApiName.ToLower() == apiName.ToLower());
                if (duplicate)
                {
                    TempData["Message"] = "Another record with that API Name already exists.";
                    TempData["MessageClass"] = "alert-warning";
                    return RedirectToAction(nameof(AdminDashboard));
                }

                entity.ApiName = apiName;
                entity.Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
                entity.DocumentationUrl = string.IsNullOrWhiteSpace(documentationUrl) ? null : documentationUrl.Trim();

                await _db.SaveChangesAsync();

                TempData["Message"] = "API Type updated.";
                TempData["MessageClass"] = "alert-success";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update API Type {Id}", id);
                TempData["Message"] = "An error occurred while updating the API Type.";
                TempData["MessageClass"] = "alert-danger";
            }

            return RedirectToAction(nameof(AdminDashboard));
        }

        // NEW: Refresh endpoint information
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RefreshApiType(int id)
        {
            try
            {
                var apiType = await _db.APITypes.FindAsync(id);
                if (apiType == null)
                {
                    TempData["Message"] = "API Type not found.";
                    TempData["MessageClass"] = "alert-warning";
                    return RedirectToAction(nameof(AdminDashboard));
                }

                if (string.IsNullOrWhiteSpace(apiType.DocumentationUrl))
                {
                    TempData["Message"] = "This API Type does not have a documentation URL.";
                    TempData["MessageClass"] = "alert-warning";
                    return RedirectToAction(nameof(AdminDashboard));
                }

                var count = await _ingest.RefreshAsync(id);
                TempData["Message"] = $"Refreshed endpoints. Imported {count} endpoints.";
                TempData["MessageClass"] = "alert-success";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to refresh API Type {Id}", id);
                TempData["Message"] = "Failed to refresh endpoints.";
                TempData["MessageClass"] = "alert-danger";
            }

            return RedirectToAction(nameof(AdminDashboard));
        }

        // NEW: Sync NFL Teams
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SyncNFLTeams()
        {
            using var client = new HttpClient();
            var response = await client.PostAsync($"{Request.Scheme}://{Request.Host}/api/Tank01/sync-nflteams", null);
            if (response.IsSuccessStatusCode)
                TempData["NFLTeamsSyncMessage"] = "NFL teams synced successfully.";
            else
                TempData["NFLTeamsSyncMessage"] = $"Sync failed: {await response.Content.ReadAsStringAsync()}";
            return RedirectToAction("AdminDashboard");
        }
    }
}