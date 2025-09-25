using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Authenticate.Data;
using Authenticate.Data.Entities;
using Authenticate.Models;
using Authenticate.Services.Testing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Authenticate.Controllers
{
    [Authorize]
    public class ApiCredentialsController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly ILogger<ApiCredentialsController> _logger;
        private readonly IDataProtector _protector;
        private readonly IHttpClientFactory _http;
        private readonly ApiCredentialTestService _testService;

        public ApiCredentialsController(
            ApplicationDbContext db,
            ILogger<ApiCredentialsController> logger,
            IDataProtectionProvider dataProtectionProvider,
            IHttpClientFactory httpClientFactory,
            ApiCredentialTestService testService)
        {
            _db = db;
            _logger = logger;
            _protector = dataProtectionProvider.CreateProtector("ApiCredentials.v1");
            _http = httpClientFactory;
            _testService = testService;
        }

        private bool TryGetUserId(out int userId)
        {
            userId = 0;
            var idStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(idStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out userId);
        }

        private string Encrypt(string plaintext) => _protector.Protect(plaintext);

        private string? Decrypt(string? ciphertext)
        {
            if (string.IsNullOrWhiteSpace(ciphertext)) return null;
            try { return _protector.Unprotect(ciphertext); }
            catch { return null; }
        }

        private static string Mask(string? secret)
        {
            if (string.IsNullOrEmpty(secret)) return "••••";
            var last4 = secret.Length >= 4 ? secret[^4..] : secret;
            return $"••••••••••••{last4}";
        }

        private async Task<List<SelectListItem>> LoadApiTypeOptionsAsync()
        {
            var types = await _db.APITypes.AsNoTracking()
                .OrderBy(a => a.ApiName)
                .Select(a => new SelectListItem { Text = a.ApiName, Value = a.Id.ToString(CultureInfo.InvariantCulture) })
                .ToListAsync();

            return types;
        }

        // GET /ApiCredentials
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            if (!TryGetUserId(out var userId)) return RedirectToAction("Login", "Account");

            var creds = await _db.ApiCredentials
                .AsNoTracking()
                .Where(c => c.UserId == userId)
                .OrderBy(c => c.Name)
                .Select(c => new
                {
                    c.Id,
                    c.Name,
                    c.BaseUrl,
                    c.AuthType,
                    c.EncryptedApiKey,
                    c.EncryptedUsername,
                    c.IsVerified,
                    ApiTypeName = c.ApiType!.ApiName,
                    ApiTypeDescription = c.ApiType!.Description
                })
                .ToListAsync();

            var ids = creds.Select(c => c.Id).ToArray();
            var cookieLookup = await _db.ApiCredentialCookies.AsNoTracking()
                .Where(x => ids.Contains(x.ApiCredentialId))
                .GroupBy(x => x.ApiCredentialId)
                .ToDictionaryAsync(g => g.Key, g => g.Select(x => x.Name).ToList());

            var model = creds.Select(c =>
            {
                string masked = "••••";
                switch (c.AuthType)
                {
                    case ApiAuthType.BearerToken:
                        masked = Mask(Decrypt(c.EncryptedApiKey));
                        break;
                    case ApiAuthType.Cookie:
                        if (cookieLookup.TryGetValue(c.Id, out var names) && names.Count > 0)
                            masked = string.Join("; ", names.Select(n => $"{n}=••••"));
                        else
                            masked = "cookie(s)=••••";
                        break;
                    case ApiAuthType.Basic:
                        var user = Decrypt(c.EncryptedUsername) ?? "user";
                        masked = $"{user}:••••";
                        break;
                }

                return new ApiCredentialEditModel
                {
                    Id = c.Id,
                    Name = c.Name,
                    BaseUrl = c.BaseUrl,
                    MaskedKey = masked,
                    IsVerified = c.IsVerified,
                    ApiTypeName = c.ApiTypeName ?? string.Empty,
                    ApiTypeDescription = c.ApiTypeDescription
                };
            }).ToList();

            return View(model);
        }

        // GET /ApiCredentials/Create
        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var vm = new ApiCredentialCreateModel
            {
                ApiTypeOptions = await LoadApiTypeOptionsAsync()
            };

            if (vm.ApiTypeOptions.Any() && vm.ApiTypeId == 0)
                vm.ApiTypeId = int.Parse(vm.ApiTypeOptions.First().Value!, CultureInfo.InvariantCulture);

            if (vm.CookiePairs.Count == 0) vm.CookiePairs.Add(new CookiePairInput());

            return View(vm);
        }

        // POST /ApiCredentials/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ApiCredentialCreateModel model)
        {
            if (!TryGetUserId(out var userId)) return RedirectToAction("Login", "Account");

            model.ApiTypeOptions = await LoadApiTypeOptionsAsync();

            // Remove validation for fields not used by the selected AuthType
            if (model.AuthType == ApiAuthType.Cookie || model.AuthType == ApiAuthType.Basic)
            {
                ModelState.Remove(nameof(model.ApiKey));
                ModelState.Remove(nameof(model.TokenHeaderName));
                ModelState.Remove(nameof(model.TokenScheme));
            }
            if (model.AuthType == ApiAuthType.BearerToken || model.AuthType == ApiAuthType.Cookie)
            {
                ModelState.Remove(nameof(model.Username));
                ModelState.Remove(nameof(model.Password));
            }

            if (!ModelState.IsValid) return View(model);

            var exists = await _db.ApiCredentials
                .AnyAsync(c => c.UserId == userId && c.Name.ToLower() == model.Name.Trim().ToLower());
            if (exists)
            {
                ModelState.AddModelError(nameof(model.Name), "A credential with this name already exists.");
                return View(model);
            }

            var apiType = await _db.APITypes.AsNoTracking().FirstOrDefaultAsync(a => a.Id == model.ApiTypeId);
            if (apiType is null)
            {
                ModelState.AddModelError(nameof(model.ApiTypeId), "Please select a valid API Type.");
                return View(model);
            }

            var isEspn = apiType.ApiName.Contains("ESPN", StringComparison.OrdinalIgnoreCase);

            // Validate per auth type
            switch (model.AuthType)
            {
                case ApiAuthType.BearerToken:
                    if (string.IsNullOrWhiteSpace(model.ApiKey) || model.ApiKey.Trim().Length < 10)
                        ModelState.AddModelError(nameof(model.ApiKey), "Token/Key must be at least 10 characters.");
                    break;
                case ApiAuthType.Cookie:
                {
                    var pairs = (model.CookiePairs ?? new())
                        .Where(p => !string.IsNullOrWhiteSpace(p.Name) && !string.IsNullOrWhiteSpace(p.Value)).ToList();
                    if (pairs.Count == 0)
                        ModelState.AddModelError("", "Add at least one cookie name/value.");
                    break;
                }
                case ApiAuthType.Basic:
                    if (string.IsNullOrWhiteSpace(model.Username))
                        ModelState.AddModelError(nameof(model.Username), "Username is required.");
                    if (string.IsNullOrWhiteSpace(model.Password))
                        ModelState.AddModelError(nameof(model.Password), "Password is required.");
                    break;
            }

            // ESPN-specific validation
            if (isEspn)
            {
                if (string.IsNullOrWhiteSpace(model.EspnSeasonId))
                    ModelState.AddModelError(nameof(model.EspnSeasonId), "ESPN Season Id is required.");
                if (string.IsNullOrWhiteSpace(model.EspnLeagueId))
                    ModelState.AddModelError(nameof(model.EspnLeagueId), "ESPN League Id is required.");
            }
            if (!ModelState.IsValid) return View(model); // FIX: added missing ')'

            var entity = new ApiCredential
            {
                Name = model.Name.Trim(),
                BaseUrl = string.IsNullOrWhiteSpace(model.BaseUrl) ? null : model.BaseUrl.Trim(),
                AuthType = model.AuthType,
                ApiTypeId = model.ApiTypeId,
                UserId = userId,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow,
                IsVerified = false,
                VerifiedAtUtc = null
            };

            if (model.AuthType == ApiAuthType.BearerToken)
            {
                entity.TokenHeaderName = string.IsNullOrWhiteSpace(model.TokenHeaderName) ? "Authorization" : model.TokenHeaderName.Trim();
                entity.TokenScheme = string.IsNullOrWhiteSpace(model.TokenScheme) ? "Bearer" : model.TokenScheme.Trim();
                entity.EncryptedApiKey = Encrypt(model.ApiKey!.Trim());
            }
            else if (model.AuthType == ApiAuthType.Cookie)
            {
                foreach (var p in (model.CookiePairs ?? new()))
                {
                    if (string.IsNullOrWhiteSpace(p.Name) || string.IsNullOrWhiteSpace(p.Value)) continue;
                    entity.Cookies.Add(new ApiCredentialCookie
                    {
                        Name = p.Name.Trim(),
                        EncryptedValue = Encrypt(p.Value.Trim())
                    });
                }
            }
            else if (model.AuthType == ApiAuthType.Basic)
            {
                entity.EncryptedUsername = Encrypt(model.Username!.Trim());
                entity.EncryptedPassword = Encrypt(model.Password!.Trim());
            }

            if (isEspn)
            {
                entity.EspnSeasonId = model.EspnSeasonId?.Trim();
                entity.EspnLeagueId = model.EspnLeagueId?.Trim();
            }

            _db.ApiCredentials.Add(entity);
            await _db.SaveChangesAsync();

            TempData["Message"] = "API credential created.";
            TempData["MessageClass"] = "alert-success";
            return RedirectToAction(nameof(Index));
        }

        // GET /ApiCredentials/Edit/{id}
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            if (!TryGetUserId(out var userId)) return RedirectToAction("Login", "Account");

            var entity = await _db.ApiCredentials
                .Include(c => c.ApiType)
                .Include(c => c.Cookies)
                .FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId);

            if (entity == null) return NotFound();

            string masked = "••••";
            switch (entity.AuthType)
            {
                case ApiAuthType.BearerToken:
                    masked = Mask(Decrypt(entity.EncryptedApiKey));
                    break;
                case ApiAuthType.Cookie:
                    masked = entity.Cookies.Count > 0
                        ? string.Join("; ", entity.Cookies.Select(x => $"{x.Name}=••••"))
                        : "cookie(s)=••••";
                    break;
                case ApiAuthType.Basic:
                    var user = Decrypt(entity.EncryptedUsername) ?? "user";
                    masked = $"{user}:••••";
                    break;
            }

            var vm = new ApiCredentialEditModel
            {
                Id = entity.Id,
                Name = entity.Name,
                BaseUrl = entity.BaseUrl,
                ApiTypeId = entity.ApiTypeId,
                ApiTypeOptions = await LoadApiTypeOptionsAsync(),
                AuthType = entity.AuthType,
                TokenHeaderName = entity.TokenHeaderName ?? "Authorization",
                TokenScheme = entity.TokenScheme ?? "Bearer",
                Username = Decrypt(entity.EncryptedUsername),
                CookiePairs = entity.Cookies.Select(c => new CookiePairInput { Name = c.Name, Value = null }).ToList(),
                MaskedKey = masked,
                IsVerified = entity.IsVerified,
                EspnSeasonId = entity.EspnSeasonId,
                EspnLeagueId = entity.EspnLeagueId,
            };

            if (vm.CookiePairs.Count == 0) vm.CookiePairs.Add(new CookiePairInput());

            return View(vm);
        }

        // POST /ApiCredentials/Edit
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(ApiCredentialEditModel model)
        {
            if (!TryGetUserId(out var userId)) return RedirectToAction("Login", "Account");

            model.ApiTypeOptions = await LoadApiTypeOptionsAsync();
            if (!ModelState.IsValid) return View(model);

            var entity = await _db.ApiCredentials
                .Include(c => c.Cookies)
                .FirstOrDefaultAsync(c => c.Id == model.Id && c.UserId == userId);
            if (entity == null) return NotFound();

            var originalBaseUrl = entity.BaseUrl ?? string.Empty;
            var originalApiTypeId = entity.ApiTypeId;
            var originalAuthType = entity.AuthType;
            var originalHeader = entity.TokenHeaderName ?? "Authorization";
            var originalScheme = entity.TokenScheme ?? "Bearer";

            entity.Name = model.Name.Trim();
            entity.BaseUrl = string.IsNullOrWhiteSpace(model.BaseUrl) ? null : model.BaseUrl.Trim();
            entity.ApiTypeId = model.ApiTypeId;
            entity.AuthType = model.AuthType;

            bool secretsChanged = false;

            entity.TokenHeaderName = string.IsNullOrWhiteSpace(model.TokenHeaderName) ? "Authorization" : model.TokenHeaderName.Trim();
            entity.TokenScheme = string.IsNullOrWhiteSpace(model.TokenScheme) ? "Bearer" : model.TokenScheme.Trim();

            if (model.AuthType == ApiAuthType.BearerToken)
            {
                if (!string.IsNullOrWhiteSpace(model.NewApiKey))
                {
                    entity.EncryptedApiKey = Encrypt(model.NewApiKey.Trim());
                    secretsChanged = true;
                }
            }

            if (model.AuthType == ApiAuthType.Cookie)
            {
                var pairs = (model.CookiePairs ?? new())
                    .Where(p => !string.IsNullOrWhiteSpace(p.Name) && !string.IsNullOrWhiteSpace(p.Value))
                    .ToList();

                if (pairs.Count > 0)
                {
                    // Replace saved set with submitted non-empty pairs
                    var existing = await _db.ApiCredentialCookies.Where(x => x.ApiCredentialId == entity.Id).ToListAsync();
                    _db.ApiCredentialCookies.RemoveRange(existing);

                    foreach (var p in pairs)
                    {
                        _db.ApiCredentialCookies.Add(new ApiCredentialCookie
                        {
                            ApiCredentialId = entity.Id,
                            Name = p.Name!.Trim(),
                            EncryptedValue = Encrypt(p.Value!.Trim())
                        });
                    }
                    secretsChanged = true;
                }
            }

            if (model.AuthType == ApiAuthType.Basic)
            {
                if (!string.IsNullOrWhiteSpace(model.Username))
                {
                    entity.EncryptedUsername = Encrypt(model.Username.Trim());
                }
                if (!string.IsNullOrWhiteSpace(model.NewPassword))
                {
                    entity.EncryptedPassword = Encrypt(model.NewPassword.Trim());
                    secretsChanged = true;
                }
            }

            // ESPN validation + mapping
            var apiType = await _db.APITypes.AsNoTracking().FirstOrDefaultAsync(a => a.Id == model.ApiTypeId);
            var isEspn = apiType != null && apiType.ApiName.Contains("ESPN", StringComparison.OrdinalIgnoreCase);

            if (isEspn)
            {
                if (string.IsNullOrWhiteSpace(model.EspnSeasonId))
                    ModelState.AddModelError(nameof(model.EspnSeasonId), "ESPN Season Id is required.");
                if (string.IsNullOrWhiteSpace(model.EspnLeagueId))
                    ModelState.AddModelError(nameof(model.EspnLeagueId), "ESPN League Id is required.");
            }
            if (!ModelState.IsValid) return View(model);

            if (isEspn)
            {
                entity.EspnSeasonId = model.EspnSeasonId?.Trim();
                entity.EspnLeagueId = model.EspnLeagueId?.Trim();
            }

            var baseUrlChanged = !string.Equals(originalBaseUrl, entity.BaseUrl ?? string.Empty, StringComparison.Ordinal);
            var apiTypeChanged = originalApiTypeId != entity.ApiTypeId;
            var authTypeChanged = originalAuthType != entity.AuthType
                                  || !string.Equals(originalHeader, entity.TokenHeaderName ?? "Authorization", StringComparison.Ordinal)
                                  || !string.Equals(originalScheme, entity.TokenScheme ?? "Bearer", StringComparison.Ordinal);

            if (baseUrlChanged || apiTypeChanged || authTypeChanged || secretsChanged)
            {
                entity.IsVerified = false;
                entity.VerifiedAtUtc = null;
            }

            entity.UpdatedAtUtc = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            TempData["Message"] = "API credential updated.";
            TempData["MessageClass"] = "alert-success";
            return RedirectToAction(nameof(Index));
        }

        // POST /ApiCredentials/Delete
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            if (!TryGetUserId(out var userId)) return RedirectToAction("Login", "Account");

            var entity = await _db.ApiCredentials.FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId);
            if (entity == null)
            {
                TempData["Message"] = "Credential not found.";
                TempData["MessageClass"] = "alert-warning";
                return RedirectToAction(nameof(Index));
            }

            // Cascade delete cookies if configured; otherwise remove manually
            var cookies = await _db.ApiCredentialCookies.Where(x => x.ApiCredentialId == id).ToListAsync();
            if (cookies.Count > 0)
                _db.ApiCredentialCookies.RemoveRange(cookies);

            _db.ApiCredentials.Remove(entity);
            await _db.SaveChangesAsync();

            TempData["Message"] = "API credential deleted.";
            TempData["MessageClass"] = "alert-success";
            return RedirectToAction(nameof(Index));
        }

        // POST /ApiCredentials/Test
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Test(int id, string? returnUrl = null)
        {
            if (!TryGetUserId(out var userId)) return RedirectToAction("Login", "Account");

            var entity = await _db.ApiCredentials
                .Include(c => c.ApiType)
                .Include(c => c.Cookies)
                .FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId);

            if (entity == null)
            {
                TempData["Message"] = "Credential not found.";
                TempData["MessageClass"] = "alert-warning";
                return SafeRedirect(returnUrl);
            }

            if (string.IsNullOrWhiteSpace(entity.BaseUrl))
            {
                TempData["Message"] = "Base URL is required to test the connection.";
                TempData["MessageClass"] = "alert-danger";
                return SafeRedirect(returnUrl);
            }

            var baseUri = new Uri(entity.BaseUrl.TrimEnd('/'), UriKind.Absolute);
            var apiTypeName = entity.ApiType?.ApiName ?? string.Empty;

            try
            {
                var tester = _testService.Resolve(entity, apiTypeName, baseUri);
                var result = await tester.TestAsync(entity, baseUri, HttpContext.RequestAborted);

                if (result.Success)
                {
                    entity.IsVerified = true;
                    entity.VerifiedAtUtc = DateTime.UtcNow;
                    await _db.SaveChangesAsync();

                    TempData["Message"] = $"Connection successful via {result.Url} ({result.StatusCode}).";
                    TempData["MessageClass"] = "alert-success";
                }
                else
                {
                    var codeText = result.StatusCode is null ? "" : $" ({result.StatusCode})";
                    TempData["Message"] = $"Could not verify connection{codeText}. {result.Message}{(string.IsNullOrWhiteSpace(result.Url) ? "" : $" – {result.Url}")}";
                    TempData["MessageClass"] = result.StatusCode is 401 or 403 ? "alert-danger" : "alert-warning";
                }
            }
            catch (TaskCanceledException)
            {
                TempData["Message"] = "Connection timed out. Verify network access and Base URL.";
                TempData["MessageClass"] = "alert-warning";
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Credential test failed for id {Id}", id);
                TempData["Message"] = "Connection test failed due to an unexpected error.";
                TempData["MessageClass"] = "alert-danger";
            }

            return SafeRedirect(returnUrl);
        }

        // POST /ApiCredentials/TestEndpoint
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TestEndpoint(int id, string endpointPath, string? method = "GET", string? returnUrl = null)
        {
            if (!TryGetUserId(out var userId)) return RedirectToAction("Login", "Account");

            var entity = await _db.ApiCredentials
                .Include(c => c.ApiType)
                .Include(c => c.Cookies)
                .FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId);

            if (entity == null)
            {
                TempData["Message"] = "Credential not found.";
                TempData["MessageClass"] = "alert-warning";
                return RedirectToAction(nameof(Index));
            }

            if (string.IsNullOrWhiteSpace(entity.BaseUrl))
            {
                TempData["Message"] = "Base URL is required to test endpoints.";
                TempData["MessageClass"] = "alert-danger";
                return SafeRedirect(returnUrl);
            }

            endpointPath = (endpointPath ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(endpointPath))
            {
                TempData["Message"] = "Please provide an endpoint path.";
                TempData["MessageClass"] = "alert-danger";
                return SafeRedirect(returnUrl);
            }

            var baseUrl = entity.BaseUrl.TrimEnd('/');
            var path = endpointPath.StartsWith("/") ? endpointPath : "/" + endpointPath;
            var url = baseUrl + path;

            var isAzureOpenAI = baseUrl.Contains(".openai.azure.com", StringComparison.OrdinalIgnoreCase);

            try
            {
                var client = _http.CreateClient();
                client.Timeout = TimeSpan.FromSeconds(15);
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                client.DefaultRequestHeaders.UserAgent.ParseAdd("AuthenticateApp/1.0");

                ApplyAuth(client, entity, isAzureOpenAI);

                HttpResponseMessage resp;
                var m = (method ?? "GET").ToUpperInvariant();
                switch (m)
                {
                    case "POST":
                        resp = await client.PostAsync(url, new StringContent("{}", Encoding.UTF8, "application/json"));
                        break;
                    case "HEAD":
                        var head = new HttpRequestMessage(HttpMethod.Head, url);
                        resp = await client.SendAsync(head);
                        break;
                    default:
                        resp = await client.GetAsync(url);
                        break;
                }

                var code = (int)resp.StatusCode;
                var ok = code >= 200 && code < 300;
                string? bodyPreview = null;
                try
                {
                    var body = await resp.Content.ReadAsStringAsync();
                    if (!string.IsNullOrWhiteSpace(body))
                        bodyPreview = body.Length > 300 ? (body[..300] + "…") : body;
                }
                catch { /* ignore */ }

                TempData["Message"] = ok
                    ? $"Endpoint test OK ({code}) {url}"
                    : $"Endpoint test failed ({code}) {url}{(string.IsNullOrWhiteSpace(bodyPreview) ? "" : " – " + bodyPreview)}";
                TempData["MessageClass"] = ok ? "alert-success" : (code == 401 || code == 403 ? "alert-danger" : "alert-warning");
            }
            catch (TaskCanceledException)
            {
                TempData["Message"] = "Endpoint test timed out.";
                TempData["MessageClass"] = "alert-warning";
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Endpoint test failed for id {Id}", id);
                TempData["Message"] = "Endpoint test failed due to an unexpected error.";
                TempData["MessageClass"] = "alert-danger";
            }

            return SafeRedirect(returnUrl);
        }

        private void ApplyAuth(HttpClient client, ApiCredential entity, bool isAzureOpenAI)
        {
            client.DefaultRequestHeaders.Remove("Authorization");
            client.DefaultRequestHeaders.Remove("api-key");
            client.DefaultRequestHeaders.Remove("Cookie");

            switch (entity.AuthType)
            {
                case ApiAuthType.BearerToken:
                {
                    var token = Decrypt(entity.EncryptedApiKey);
                    if (string.IsNullOrWhiteSpace(token)) return;

                    var headerName = (entity.TokenHeaderName ?? "Authorization").Trim();
                    var scheme = (entity.TokenScheme ?? "Bearer").Trim();

                    if (isAzureOpenAI)
                    {
                        headerName = "api-key";
                        scheme = string.Empty;
                    }

                    if (headerName.Equals("Authorization", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!string.IsNullOrWhiteSpace(scheme))
                            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(scheme, token);
                        else
                            client.DefaultRequestHeaders.Add("Authorization", token);
                    }
                    else
                    {
                        client.DefaultRequestHeaders.Remove(headerName);
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

        private IActionResult SafeRedirect(string? returnUrl)
            => !string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl)
                ? Redirect(returnUrl)
                : RedirectToAction(nameof(Index));

        // Local helper to avoid CS1503
        private static string Mask(string? key, bool ed = false) => Mask(key);
        private static string Mask(string? key, string? ed = null) => Mask(key);
        private static string Mask(string? key, object? ed = null) => Mask(key);
        private static string Mask(string? key, int ed = 0) => Mask(key);
        private static string Mask(string? key, double ed = 0) => Mask(key);
        private static string Mask(string? key, decimal ed = 0) => Mask(key);
        private static string Mask(string? key, DateTime? ed = null) => Mask(key);
        private static string Mask(string? key, DateOnly? ed = null) => Mask(key);
        private static string Mask(string? key, TimeOnly? ed = null) => Mask(key);
        private static string Mask(string? key, Guid? ed = null) => Mask(key);
        private static string Mask(string? key, byte? ed = null) => Mask(key);
    }
}