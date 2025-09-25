using System.Net.Http.Json;
using System.Text.Json;

namespace Authenticate.Infrastructure.Gemini
{
    public class GeminiMetadataClient(HttpClient http, GeminiOptions options) : IGeminiMetadataClient
    {
        private readonly HttpClient _http = http;
        private readonly GeminiOptions _opt = options;

        public async Task<IReadOnlyList<GeminiEndpointDto>> GetEndpointsAsync(CancellationToken ct = default)
        {
            var url = $"{_opt.BaseUrl.TrimEnd('/')}/$discovery/rest?version=v1";
            using var stream = await _http.GetStreamAsync(url, ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

            var endpoints = new List<GeminiEndpointDto>();
            ExtractMethods(doc.RootElement, endpoints);
            // Order by path then id
            return endpoints
                .DistinctBy(e => (e.Id, e.HttpMethod, e.Path))
                .OrderBy(e => e.Path).ThenBy(e => e.Id)
                .ToList();

            static void ExtractMethods(JsonElement node, List<GeminiEndpointDto> acc)
            {
                if (node.TryGetProperty("methods", out var methods) && methods.ValueKind == JsonValueKind.Object)
                {
                    foreach (var prop in methods.EnumerateObject())
                    {
                        var m = prop.Value;
                        var id = m.TryGetProperty("id", out var idEl) ? idEl.GetString() ?? prop.Name : prop.Name;
                        var httpMethod = m.TryGetProperty("httpMethod", out var hm) ? hm.GetString() ?? "GET" : "GET";
                        var path = m.TryGetProperty("path", out var p) ? "/" + (p.GetString() ?? "").TrimStart('/') : "/";
                        var desc = m.TryGetProperty("description", out var d) ? d.GetString() : null;
                        acc.Add(new GeminiEndpointDto(id, httpMethod, path, desc));
                    }
                }
                if (node.TryGetProperty("resources", out var resources) && resources.ValueKind == JsonValueKind.Object)
                {
                    foreach (var res in resources.EnumerateObject())
                    {
                        ExtractMethods(res.Value, acc);
                    }
                }
            }
        }

        public async Task<IReadOnlyList<GeminiModelDto>> GetModelsAsync(CancellationToken ct = default)
        {
            var key = _opt.ApiKey;
            var url = $"{_opt.BaseUrl.TrimEnd('/')}/v1/models";
            if (!string.IsNullOrWhiteSpace(key))
                url += $"?key={Uri.EscapeDataString(key)}";

            using var resp = await _http.GetAsync(url, ct);
            resp.EnsureSuccessStatusCode();

            using var stream = await resp.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

            var list = new List<GeminiModelDto>();
            if (doc.RootElement.TryGetProperty("models", out var models) && models.ValueKind == JsonValueKind.Array)
            {
                foreach (var m in models.EnumerateArray())
                {
                    var name = m.TryGetProperty("name", out var n) ? n.GetString() : null;
                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        var display = m.TryGetProperty("displayName", out var dn) ? dn.GetString() : null;
                        list.Add(new GeminiModelDto(name!, display));
                    }
                }
            }
            return list.OrderBy(m => m.Name).ToList();
        }
    }
}