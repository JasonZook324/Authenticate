using Authenticate.Infrastructure.Gemini;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Authenticate.Controllers
{
    [Authorize]
    public class GeminiController(IGeminiMetadataClient meta, ILogger<GeminiController> logger) : Controller
    {
        private readonly IGeminiMetadataClient _meta = meta;
        private readonly ILogger<GeminiController> _logger = logger;

        [HttpGet("/Gemini/Endpoints")]
        public async Task<IActionResult> Endpoints()
        {
            try
            {
                var items = await _meta.GetEndpointsAsync();
                return Json(items);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch Gemini endpoints");
                return StatusCode(500, new { error = "Failed to fetch endpoints" });
            }
        }

        [HttpGet("/Gemini/Models")]
        public async Task<IActionResult> Models()
        {
            try
            {
                var items = await _meta.GetModelsAsync();
                return Json(items);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch Gemini models");
                return StatusCode(500, new { error = "Failed to fetch models" });
            }
        }
    }
}