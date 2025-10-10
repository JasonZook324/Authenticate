using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Authenticate.Data;
using Authenticate.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Authenticate.Controllers
{
    [Authorize]
    public class PlaygroundController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly IHttpClientFactory _http;
        private readonly ILogger<PlaygroundController> _logger;
        private readonly IDataProtector _protector;

        public PlaygroundController(ApplicationDbContext db, IHttpClientFactory http, IDataProtectionProvider dp, ILogger<PlaygroundController> logger)
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

        private async Task<List<SelectListItem>> LoadCredsAsync(int userId, int? selectedId, ApiPlaygroundViewModel? vm = null)
        {
            var list = await _db.ApiCredentials
                .Include(c => c.ApiType)
                .Include(c => c.Cookies)
                .Where(c => c.UserId == userId)
                .OrderBy(c => c.Name)
                .Select(c => new { c.Id, c.Name, ApiName = c.ApiType != null ? c.ApiType.ApiName : null, c.BaseUrl, c.EspnSeasonId, c.EspnLeagueId })
                .ToListAsync();

            vm?.CredentialApiTypes.Clear();
            vm?.CredentialExtras.Clear();
            foreach (var c in list)
            {
                if (vm != null && c.ApiName != null)
                {
                    vm.CredentialApiTypes[c.Id] = c.ApiName;
                    vm.CredentialExtras[c.Id] = new CredentialExtras
                    {
                        ApiName = c.ApiName,
                        BaseUrl = c.BaseUrl,
                        EspnSeasonId = c.EspnSeasonId,
                        EspnLeagueId = c.EspnLeagueId
                    };
                }
            }

            return list
                .Select(c => new SelectListItem
                {
                    Text = c.ApiName != null ? $"{c.Name} ({c.ApiName})" : c.Name,
                    Value = c.Id.ToString(),
                    Selected = selectedId.HasValue && selectedId.Value == c.Id
                })
                .ToList();
        }

        [HttpGet]
        public async Task<IActionResult> Index(int? credId = null)
        {
            if (!TryGetUserId(out var userId)) return RedirectToAction("Login", "Account");

            var vm = new ApiPlaygroundViewModel
            {
                Method = "GET",
                BaseUrl = "https://api.example.com",
                EndpointPath = "/v1/models",
                Query = null,
                UseCredentialAuth = true
            };

            vm.AvailableCredentials = await LoadCredsAsync(userId, credId, vm);

            if (credId.HasValue)
                vm.SelectedCredentialId = credId;

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(ApiPlaygroundViewModel vm)
        {
            if (!TryGetUserId(out var userId)) return RedirectToAction("Login", "Account");
            vm.AvailableCredentials = await LoadCredsAsync(userId, vm.SelectedCredentialId, vm);

            try
            {
                if (string.IsNullOrWhiteSpace(vm.BaseUrl))
                {
                    vm.Error = "Base URL is required.";
                    return View(vm);
                }

                var baseUri = new Uri(vm.BaseUrl.TrimEnd('/'), UriKind.Absolute);
                var path = (vm.EndpointPath ?? string.Empty).Trim();
                if (!path.StartsWith("/")) path = "/" + path;
                var url = new Uri(baseUri, path).ToString();
                if (!string.IsNullOrWhiteSpace(vm.Query))
                {
                    url += (url.Contains("?") ? "&" : "?") + vm.Query.TrimStart('?');
                }
                vm.ResolvedUrl = url;

                using var client = _http.CreateClient();
                client.Timeout = TimeSpan.FromSeconds(30);
                client.DefaultRequestHeaders.UserAgent.ParseAdd("AuthenticateApp/1.0");
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                // Apply request headers entered by user
                if (!string.IsNullOrWhiteSpace(vm.HeadersRaw))
                {
                    var lines = vm.HeadersRaw.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    foreach (var line in lines)
                    {
                        var idx = line.IndexOf(':');
                        if (idx > 0)
                        {
                            var name = line.Substring(0, idx).Trim();
                            var value = line.Substring(idx + 1).Trim();
                            if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(value))
                            {
                                client.DefaultRequestHeaders.Remove(name);
                                client.DefaultRequestHeaders.TryAddWithoutValidation(name, value);
                            }
                        }
                    }
                }

                // Optionally apply credential auth (bearer/custom header, cookie(s), or basic)
                if (vm.UseCredentialAuth && vm.SelectedCredentialId.HasValue)
                {
                    var cred = await _db.ApiCredentials
                        .Include(c => c.Cookies)
                        .Include(c => c.ApiType)
                        .FirstOrDefaultAsync(c => c.Id == vm.SelectedCredentialId && c.UserId == userId);

                    if (cred != null)
                    {
                        switch (cred.AuthType)
                        {
                            case Data.Entities.ApiAuthType.BearerToken:
                            {
                                var header = (cred.TokenHeaderName ?? "Authorization").Trim();
                                var scheme = (cred.TokenScheme ?? "Bearer").Trim();
                                var token = Decrypt(cred.EncryptedApiKey);
                                if (!string.IsNullOrWhiteSpace(token))
                                {
                                    client.DefaultRequestHeaders.Remove(header);
                                    var val = string.IsNullOrWhiteSpace(scheme) ? token : $"{scheme} {token}";
                                    client.DefaultRequestHeaders.TryAddWithoutValidation(header, val);
                                }
                                break;
                            }
                            case Data.Entities.ApiAuthType.Cookie:
                            {
                                if (cred.Cookies.Count > 0)
                                {
                                    var pairs = new List<string>(cred.Cookies.Count);
                                    foreach (var c in cred.Cookies)
                                    {
                                        var val = Decrypt(c.EncryptedValue);
                                        if (string.IsNullOrWhiteSpace(c.Name) || string.IsNullOrWhiteSpace(val)) continue;
                                        pairs.Add($"{c.Name}={val}");
                                    }
                                    if (pairs.Count > 0)
                                    {
                                        client.DefaultRequestHeaders.Remove("Cookie");
                                        client.DefaultRequestHeaders.TryAddWithoutValidation("Cookie", string.Join("; ", pairs));
                                    }
                                }
                                break;
                            }
                            case Data.Entities.ApiAuthType.Basic:
                            {
                                var userName = Decrypt(cred.EncryptedUsername) ?? string.Empty;
                                var pass = Decrypt(cred.EncryptedPassword) ?? string.Empty;
                                var bytes = Encoding.UTF8.GetBytes($"{userName}:{pass}");
                                var b64 = Convert.ToBase64String(bytes);
                                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", b64);
                                break;
                            }
                        }
                    }
                }

                var sw = Stopwatch.StartNew();
                HttpResponseMessage resp;
                switch ((vm.Method ?? "GET").ToUpperInvariant())
                {
                    case "POST":
                        resp = await client.PostAsync(url, new StringContent(string.IsNullOrWhiteSpace(vm.BodyJson) ? "{}" : vm.BodyJson, Encoding.UTF8, "application/json"));
                        break;
                    case "PUT":
                        resp = await client.PutAsync(url, new StringContent(vm.BodyJson ?? "{}", Encoding.UTF8, "application/json"));
                        break;
                    case "PATCH":
                        resp = await client.SendAsync(new HttpRequestMessage(new HttpMethod("PATCH"), url)
                        {
                            Content = new StringContent(vm.BodyJson ?? "{}", Encoding.UTF8, "application/json")
                        });
                        break;
                    case "DELETE":
                        resp = await client.DeleteAsync(url);
                        break;
                    case "HEAD":
                        resp = await client.SendAsync(new HttpRequestMessage(HttpMethod.Head, url));
                        break;
                    case "OPTIONS":
                        resp = await client.SendAsync(new HttpRequestMessage(HttpMethod.Options, url));
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
                vm.ResponseBody = body;

                try
                {
                    using var doc = JsonDocument.Parse(body);
                    vm.ResponseBodyPretty = JsonSerializer.Serialize(doc, new JsonSerializerOptions { WriteIndented = true });
                }
                catch
                {
                    // not JSON; leave as-is
                }

                if (!resp.IsSuccessStatusCode)
                    vm.Error = $"Request failed ({vm.StatusCode}).";
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "API playground call failed");
                vm.Error = "Call failed. Check inputs and try again.";
            }

            return View(vm);
        }
    }
}
