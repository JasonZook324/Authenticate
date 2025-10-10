using System.Net.Http.Headers;
using System.Security.Claims;
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
    public class RosterController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly IHttpClientFactory _http;
        private readonly IDataProtector _protector;
        private readonly ILogger<RosterController> _logger;

        public RosterController(ApplicationDbContext db, IHttpClientFactory http, IDataProtectionProvider dp, ILogger<RosterController> logger)
        {
            _db = db;
            _http = http;
            _protector = dp.CreateProtector("ApiCredentials.v1");
            _logger = logger;
        }

        private bool TryGetUserId(out int userId)
        {
            userId = 0;
            var idStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(idStr, out userId);
        }

        private string? Decrypt(string? cipher)
        {
            if (string.IsNullOrWhiteSpace(cipher)) return null;
            try { return _protector.Unprotect(cipher); } catch { return null; }
        }

        private void ApplyAuth(HttpClient client, ApiCredential entity)
        {
            client.DefaultRequestHeaders.Remove("Authorization");
            client.DefaultRequestHeaders.Remove("Cookie");

            switch (entity.AuthType)
            {
                case ApiAuthType.BearerToken:
                {
                    var token = Decrypt(entity.EncryptedApiKey);
                    if (string.IsNullOrWhiteSpace(token)) return;

                    var headerName = (entity.TokenHeaderName ?? "Authorization").Trim();
                    var scheme = (entity.TokenScheme ?? "Bearer").Trim();

                    if (headerName.Equals("Authorization", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!string.IsNullOrWhiteSpace(scheme))
                            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(scheme, token);
                        else
                            client.DefaultRequestHeaders.Add("Authorization", token);
                    }
                    else
                    {
                        var value = string.IsNullOrWhiteSpace(scheme) ? token : $"{scheme} {token}";
                        client.DefaultRequestHeaders.Add(headerName, value);
                    }
                    break;
                }
                case ApiAuthType.Cookie:
                {
                    if (entity.Cookies.Count == 0) return;
                    var pairs = new List<string>(entity.Cookies.Count);
                    foreach (var c in entity.Cookies)
                    {
                        var val = Decrypt(c.EncryptedValue);
                        if (string.IsNullOrWhiteSpace(c.Name) || string.IsNullOrWhiteSpace(val)) continue;
                        pairs.Add($"{c.Name}={val}");
                    }
                    if (pairs.Count > 0)
                        client.DefaultRequestHeaders.Add("Cookie", string.Join("; ", pairs));
                    break;
                }
                case ApiAuthType.Basic:
                {
                    var user = Decrypt(entity.EncryptedUsername) ?? string.Empty;
                    var pass = Decrypt(entity.EncryptedPassword) ?? string.Empty;
                    var bytes = Encoding.UTF8.GetBytes($"{user}:{pass}");
                    var b64 = Convert.ToBase64String(bytes);
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", b64);
                    break;
                }
            }
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var vm = new RosterViewModel();
            if (!TryGetUserId(out var userId))
            {
                TempData["Message"] = "Please sign in.";
                TempData["MessageClass"] = "alert-warning";
                return RedirectToAction("Login", "Account");
            }

            var cred = await _db.ApiCredentials
                .Include(c => c.ApiType)
                .Include(c => c.Cookies)
                .AsNoTracking()
                .Where(c => c.UserId == userId && c.ApiType != null && c.ApiType.ApiName.ToLower().Contains("espn"))
                .FirstOrDefaultAsync();

            if (cred == null)
            {
                vm.Error = "No ESPN credential found. Create one first.";
                return View(vm);
            }

            if (string.IsNullOrWhiteSpace(cred.BaseUrl) || string.IsNullOrWhiteSpace(cred.EspnSeasonId) || string.IsNullOrWhiteSpace(cred.EspnLeagueId))
            {
                vm.Error = "ESPN credentials are missing BaseUrl, Season Id, or League Id.";
                return View(vm);
            }

            try
            {
                var (league, err) = await GetLeagueAsync(cred);
                if (err != null)
                {
                    vm.Error = err;
                    return View(vm);
                }

                vm.Teams = (league!.Teams ?? new())
                    .OrderBy(t => t.Name ?? t.Abbrev ?? string.Empty)
                    .Select(t => new SelectListItem
                    {
                        Text = $"{t.Name ?? "Team"} ({t.Abbrev ?? "N/A"})",
                        Value = t.Id?.ToString() ?? ""
                    })
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load ESPN teams for user {UserId}", userId);
                vm.Error = "Could not load teams from ESPN.";
            }

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(RosterViewModel vm)
        {
            if (!TryGetUserId(out var userId))
            {
                TempData["Message"] = "Please sign in.";
                TempData["MessageClass"] = "alert-warning";
                return RedirectToAction("Login", "Account");
            }

            var cred = await _db.ApiCredentials
                .Include(c => c.ApiType)
                .Include(c => c.Cookies)
                .AsNoTracking()
                .Where(c => c.UserId == userId && c.ApiType != null && c.ApiType.ApiName.ToLower().Contains("espn"))
                .FirstOrDefaultAsync();

            if (cred == null)
            {
                vm.Error = "No ESPN credential found. Create one first.";
                return View(vm);
            }

            if (string.IsNullOrWhiteSpace(cred.BaseUrl) || string.IsNullOrWhiteSpace(cred.EspnSeasonId) || string.IsNullOrWhiteSpace(cred.EspnLeagueId))
            {
                vm.Error = "ESPN credentials are missing BaseUrl, Season Id, or League Id.";
                return View(vm);
            }

            try
            {
                var (league, err) = await GetLeagueAsync(cred);
                if (err != null)
                {
                    vm.Error = err;
                    return View(vm);
                }

                // Repopulate teams for the dropdown
                vm.Teams = (league!.Teams ?? new())
                    .OrderBy(t => t.Name ?? t.Abbrev ?? string.Empty)
                    .Select(t => new SelectListItem
                    {
                        Text = $"{t.Name ?? "Team"} ({t.Abbrev ?? "N/A"})",
                        Value = t.Id?.ToString() ?? ""
                    })
                    .ToList();

                if (!vm.TeamId.HasValue)
                {
                    vm.Error = "No team selected.";
                    return View(vm);
                }

                var team = league.Teams?.FirstOrDefault(t => t.Id == vm.TeamId);
                if (team == null)
                {
                    vm.Error = "Selected team not found.";
                    return View(vm);
                }

                // Parse lineup entries
                var entries = team.Roster?.Entries ?? new();
                vm.RosterEntries = entries
                    .Select(e => new RosterEntry
                    {
                        Slot = SlotName(e.LineupSlotId),
                        PlayerName = e.PlayerPoolEntry?.Player?.FullName ?? $"Player {e.PlayerPoolEntry?.Player?.Id}"
                    })
                    .OrderBy(e => e.Slot)
                    .ToList();

                vm.SelectedTeamName = $"{team.Name ?? "Team"} ({team.Abbrev ?? "N/A"})";
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load ESPN roster for user {UserId}", userId);
                vm.Error = "Unable to fetch lineup data. Please try again later.";
            }

            return View(vm);
        }

        // Helper: call ESPN mRoster using the saved ESPN credential (uses Cookie auth if configured)
        private async Task<(EspnLeagueDto? league, string? error)> GetLeagueAsync(ApiCredential cred)
        {
            var baseUri = new Uri(cred.BaseUrl!.TrimEnd('/'), UriKind.Absolute);
            var path = $"/apis/v3/games/ffl/seasons/{cred.EspnSeasonId!.Trim()}/segments/0/leagues/{cred.EspnLeagueId!.Trim()}?view=mRoster";
            var url = new Uri(baseUri, path).ToString();

            using var client = _http.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(15);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("AuthenticateApp/1.0");
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            // Cookie header e.g. "SWID=...; espn_s2=..."
            ApplyAuth(client, cred);

            using var resp = await client.GetAsync(url);
            var body = await resp.Content.ReadAsStringAsync();
            if (!resp.IsSuccessStatusCode)
                return (null, $"Unable to fetch lineup data from ESPN ({(int)resp.StatusCode}).");

            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var league = JsonSerializer.Deserialize<EspnLeagueDto>(body, options) ?? new EspnLeagueDto();
            return (league, null);
        }

        private static string SlotName(int? id) => id switch
        {
            0 => "QB",
            2 => "RB",
            3 => "RB/WR",
            4 => "WR",
            5 => "WR/TE",
            6 => "TE",
            7 => "OP",
            16 => "D/ST",
            17 => "K",
            20 => "Bench",
            21 => "IR",
            23 => "FLEX",
            _ => id.HasValue ? $"Slot {id}" : "Slot"
        };

        // Minimal DTOs for ESPN response (mRoster)
        private sealed class EspnLeagueDto
        {
            public List<EspnTeamDto>? Teams { get; set; }
        }
        private sealed class EspnTeamDto
        {
            public int? Id { get; set; }
            public string? Name { get; set; }
            public string? Abbrev { get; set; }
            public EspnRosterDto? Roster { get; set; }
        }
        private sealed class EspnRosterDto
        {
            public List<EspnRosterEntryDto>? Entries { get; set; }
        }
        private sealed class EspnRosterEntryDto
        {
            public int? LineupSlotId { get; set; }
            public EspnPlayerPoolEntryDto? PlayerPoolEntry { get; set; }
        }
        private sealed class EspnPlayerPoolEntryDto
        {
            public EspnPlayerDto? Player { get; set; }
        }
        private sealed class EspnPlayerDto
        {
            public int? Id { get; set; }
            public string? FullName { get; set; }
        }
    }
}