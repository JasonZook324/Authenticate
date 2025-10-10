using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Authenticate.Data;
using Authenticate.Data.Entities;
using Authenticate.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Authenticate.Controllers
{
    [Authorize]
    public class EspnController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly IHttpClientFactory _http;
        private readonly IDataProtector _protector;
        private readonly ILogger<EspnController> _logger;

        public EspnController(ApplicationDbContext db, IHttpClientFactory http, IDataProtectionProvider dp, ILogger<EspnController> logger)
        {
            _db = db;
            _http = http;
            _protector = dp.CreateProtector("ApiCredentials.v1");
            _logger = logger;
        }

        private bool TryGetUserId(out int userId)
        {
            userId = 0;
            var idStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(idStr, out userId);
        }

        private string? Decrypt(string? cipher)
        {
            if (string.IsNullOrWhiteSpace(cipher)) return null;
            try { return _protector.Unprotect(cipher); } catch { return null; }
        }

        private async Task<List<SelectListItem>> LoadEspnCredsAsync(int userId, int? selectedId)
        {
            var creds = await _db.ApiCredentials
                .Include(c => c.ApiType)
                .Where(c => c.UserId == userId && c.ApiType != null && c.ApiType.ApiName.ToLower().Contains("espn"))
                .OrderBy(c => c.Name)
                .Select(c => new SelectListItem
                {
                    Text = c.Name,
                    Value = c.Id.ToString(),
                    Selected = selectedId.HasValue && selectedId.Value == c.Id
                })
                .ToListAsync();
            return creds;
        }

        [HttpGet]
        public async Task<IActionResult> Playground(int? credId = null)
        {
            if (!TryGetUserId(out var userId)) return RedirectToAction("Login", "Account");

            var vm = new EspnApiPlaygroundViewModel();
            vm.AvailableCredentials = await LoadEspnCredsAsync(userId, credId);

            ApiCredential? cred = null;
            if (credId.HasValue)
            {
                cred = await _db.ApiCredentials
                    .Include(c => c.Cookies)
                    .FirstOrDefaultAsync(c => c.Id == credId && c.UserId == userId);
            }
            else
            {
                var first = vm.AvailableCredentials.FirstOrDefault();
                if (first != null && int.TryParse(first.Value, out var firstId))
                {
                    cred = await _db.ApiCredentials
                        .Include(c => c.Cookies)
                        .FirstOrDefaultAsync(c => c.Id == firstId && c.UserId == userId);
                }
            }

            if (cred != null)
            {
                vm.SelectedCredentialId = cred.Id;
                vm.BaseUrl = cred.BaseUrl ?? "https://lm-api-reads.fantasy.espn.com";
                vm.SeasonId = cred.EspnSeasonId ?? "";
                vm.LeagueId = cred.EspnLeagueId ?? "";
            }
            else
            {
                vm.Error = "No ESPN credentials found. Create one first.";
                vm.BaseUrl = "https://lm-api-reads.fantasy.espn.com";
            }

            // Sensible defaults
            vm.EndpointPath = "/apis/v3/games/ffl/seasons/{seasonId}/segments/0/leagues/{leagueId}";
            vm.Query = "view=mRoster";
            vm.Method = "GET";
            vm.UseCredentialCookies = true;

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Playground(EspnApiPlaygroundViewModel vm)
        {
            if (!TryGetUserId(out var userId)) return RedirectToAction("Login", "Account");

            vm.AvailableCredentials = await LoadEspnCredsAsync(userId, vm.SelectedCredentialId);

            if (string.IsNullOrWhiteSpace(vm.BaseUrl))
            {
                vm.Error = "Base URL is required.";
                return View(vm);
            }

            ApiCredential? cred = null;
            if (vm.SelectedCredentialId.HasValue)
            {
                cred = await _db.ApiCredentials
                    .Include(c => c.Cookies)
                    .FirstOrDefaultAsync(c => c.Id == vm.SelectedCredentialId && c.UserId == userId);
            }

            try
            {
                var baseUri = new Uri(vm.BaseUrl.TrimEnd('/'), UriKind.Absolute);
                var path = (vm.EndpointPath ?? "").Trim();
                path = path.Replace("{seasonId}", vm.SeasonId ?? "", StringComparison.OrdinalIgnoreCase)
                           .Replace("{leagueId}", vm.LeagueId ?? "", StringComparison.OrdinalIgnoreCase);
                if (!path.StartsWith("/")) path = "/" + path;

                // Merge query
                var url = new Uri(baseUri, path).ToString();
                if (!string.IsNullOrWhiteSpace(vm.Query))
                {
                    var hasQuery = url.Contains("?");
                    url += (hasQuery ? "&" : "?") + vm.Query.TrimStart('?');
                }
                vm.ResolvedUrl = url;

                using var client = _http.CreateClient();
                client.Timeout = TimeSpan.FromSeconds(20);
                client.DefaultRequestHeaders.UserAgent.ParseAdd("AuthenticateApp/1.0");
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                // Cookies: from credential or manual SWID/espn_s2
                if (vm.UseCredentialCookies && cred != null && cred.Cookies.Count > 0)
                {
                    var pairs = cred.Cookies
                        .Select(c => new { c.Name, Val = Decrypt(c.EncryptedValue) })
                        .Where(x => !string.IsNullOrWhiteSpace(x.Name) && !string.IsNullOrWhiteSpace(x.Val))
                        .Select(x => $"{x.Name}={x.Val}");
                    var cookieHeader = string.Join("; ", pairs);
                    if (!string.IsNullOrWhiteSpace(cookieHeader))
                        client.DefaultRequestHeaders.Add("Cookie", cookieHeader);
                }
                else if (!string.IsNullOrWhiteSpace(vm.SWID) || !string.IsNullOrWhiteSpace(vm.EspnS2))
                {
                    var parts = new List<string>();
                    if (!string.IsNullOrWhiteSpace(vm.SWID)) parts.Add($"SWID={vm.SWID!.Trim()}");
                    if (!string.IsNullOrWhiteSpace(vm.EspnS2)) parts.Add($"espn_s2={vm.EspnS2!.Trim()}");
                    if (parts.Count > 0)
                        client.DefaultRequestHeaders.Add("Cookie", string.Join("; ", parts));
                }

                var sw = Stopwatch.StartNew();
                HttpResponseMessage resp;
                switch ((vm.Method ?? "GET").ToUpperInvariant())
                {
                    case "POST":
                        var content = new StringContent(string.IsNullOrWhiteSpace(vm.BodyJson) ? "{}" : vm.BodyJson, Encoding.UTF8, "application/json");
                        resp = await client.PostAsync(url, content);
                        break;
                    case "HEAD":
                        var head = new HttpRequestMessage(HttpMethod.Head, url);
                        resp = await client.SendAsync(head);
                        break;
                    default:
                        resp = await client.GetAsync(url);
                        break;
                }
                sw.Stop();

                vm.StatusCode = (int)resp.StatusCode;
                vm.ElapsedMs = sw.ElapsedMilliseconds;
                vm.ResponseHeaders = string.Join("\n", resp.Headers.Select(h => $"{h.Key}: {string.Join(", ", h.Value)}")
                    .Concat(resp.Content.Headers.Select(h => $"{h.Key}: {string.Join(", ", h.Value)}")));

                var body = await resp.Content.ReadAsStringAsync();
                vm.ResponseBody = body; // ensure raw body available for explorer

                // Try pretty JSON
                try
                {
                    using var doc = JsonDocument.Parse(body);
                    vm.ResponseBodyPretty = JsonSerializer.Serialize(doc, new JsonSerializerOptions { WriteIndented = true });
                    // Optional: extract subsets, etc.
                }
                catch { /* not JSON */ }

                if (!resp.IsSuccessStatusCode)
                    vm.Error = $"Request failed ({vm.StatusCode}).";
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "ESPN API playground call failed");
                vm.Error = "Call failed. Check inputs and try again.";
            }

            return View(vm);
        }
    }
}