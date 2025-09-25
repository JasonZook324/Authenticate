using System.Text.Json;
using Authenticate.Data;
using Authenticate.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Readers;

namespace Authenticate.Infrastructure.ApiDocs
{
    public class OpenApiIngestionService : IApiDocIngestionService
    {
        private readonly ApplicationDbContext _db;
        private readonly IHttpClientFactory _httpFactory;
        private readonly ILogger<OpenApiIngestionService> _logger;

        public OpenApiIngestionService(ApplicationDbContext db, IHttpClientFactory httpFactory, ILogger<OpenApiIngestionService> logger)
        {
            _db = db;
            _httpFactory = httpFactory;
            _logger = logger;
        }

        public async Task<int> RefreshAsync(int apiTypeId, CancellationToken ct = default)
        {
            var apiType = await _db.APITypes.FirstOrDefaultAsync(a => a.Id == apiTypeId, ct);
            if (apiType == null || string.IsNullOrWhiteSpace(apiType.DocumentationUrl))
                return 0;

            // Delete existing endpoints for this API type
            var existing = _db.ApiTypeEndpoints.Where(e => e.ApiTypeId == apiTypeId);
            _db.ApiTypeEndpoints.RemoveRange(existing);
            await _db.SaveChangesAsync(ct);

            var count = await IngestAsync(apiTypeId, apiType.DocumentationUrl, ct);
            return count;
        }

        public async Task<int> IngestAsync(int apiTypeId, string documentationUrl, CancellationToken ct = default)
        {
            var client = _httpFactory.CreateClient();
            using var resp = await client.GetAsync(documentationUrl, ct);
            resp.EnsureSuccessStatusCode();
            var contentType = resp.Content.Headers.ContentType?.MediaType?.ToLowerInvariant() ?? "application/json";
            await using var stream = await resp.Content.ReadAsStreamAsync(ct);

            int saved = 0;

            // Try OpenAPI
            if (contentType.Contains("yaml") || contentType.Contains("yml") || contentType.Contains("json"))
            {
                try
                {
                    var reader = new OpenApiStreamReader();
                    var doc = reader.Read(stream, out var diagnostic);
                    if (diagnostic?.Errors?.Count > 0)
                    {
                        _logger.LogInformation("OpenAPI diagnostics: {Errors}", string.Join("; ", diagnostic.Errors.Select(e => e.Message)));
                    }

                    if (doc?.Paths != null && doc.Paths.Count > 0)
                    {
                        saved = await SaveFromOpenApiAsync(apiTypeId, doc, ct);
                        await UpdateLastSynced(apiTypeId, ct);
                        return saved;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogInformation(ex, "Failed to parse OpenAPI; will try Discovery.");
                }

                // If OpenAPI parse failed, rewind stream or refetch for discovery parse
                stream.Dispose();
            }

            // Try Google Discovery format
            using (var resp2 = await client.GetAsync(documentationUrl, ct))
            {
                resp2.EnsureSuccessStatusCode();
                var json = await resp2.Content.ReadAsStringAsync(ct);
                var doc = JsonDocument.Parse(json);
                if (doc.RootElement.ValueKind == JsonValueKind.Object)
                {
                    saved = await SaveFromGoogleDiscoveryAsync(apiTypeId, doc.RootElement, ct);
                    await UpdateLastSynced(apiTypeId, ct);
                    return saved;
                }
            }

            return saved;
        }

        private async Task UpdateLastSynced(int apiTypeId, CancellationToken ct)
        {
            var apiType = await _db.APITypes.FirstAsync(a => a.Id == apiTypeId, ct);
            apiType.LastSyncedUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
        }

        private async Task<int> SaveFromOpenApiAsync(int apiTypeId, OpenApiDocument doc, CancellationToken ct)
        {
            var endpoints = new List<ApiTypeEndpoint>();

            foreach (var pathKvp in doc.Paths)
            {
                var path = pathKvp.Key;
                var item = pathKvp.Value;

                foreach (var (method, op) in item.Operations)
                {
                    var ep = new ApiTypeEndpoint
                    {
                        ApiTypeId = apiTypeId,
                        Method = method.ToString().ToUpperInvariant(),
                        Path = path,
                        OperationId = op.OperationId,
                        Summary = op.Summary,
                        Description = op.Description,
                        Deprecated = op.Deprecated
                    };

                    // Parameters (path, query, header, cookie)
                    var allParams = new List<OpenApiParameter>();
                    if (item.Parameters != null) allParams.AddRange(item.Parameters);
                    if (op.Parameters != null) allParams.AddRange(op.Parameters);
                    foreach (var p in allParams)
                    {
                        ep.Parameters.Add(new ApiTypeEndpointParameter
                        {
                            Name = p.Name,
                            In = p.In.ToString(),
                            Required = p.Required,
                            Type = p.Schema?.Type,
                            Format = p.Schema?.Format,
                            SchemaJson = TrySchemaToJson(p.Schema)
                        });
                    }

                    // Request body as "body" parameter
                    if (op.RequestBody != null)
                    {
                        var rb = op.RequestBody;
                        // Prefer application/json
                        var media = rb.Content?.FirstOrDefault().Value;
                        var schema = media?.Schema;

                        ep.Parameters.Add(new ApiTypeEndpointParameter
                        {
                            Name = "body",
                            In = "body",
                            Required = rb.Required,
                            Type = schema?.Type,
                            Format = schema?.Format,
                            SchemaJson = TrySchemaToJson(schema)
                        });
                    }

                    // Responses
                    if (op.Responses != null)
                    {
                        foreach (var (status, response) in op.Responses)
                        {
                            if (response.Content != null && response.Content.Count > 0)
                            {
                                foreach (var (cty, media) in response.Content)
                                {
                                    ep.Responses.Add(new ApiTypeEndpointResponse
                                    {
                                        StatusCode = status,
                                        ContentType = cty,
                                        Type = media.Schema?.Type,
                                        Format = media.Schema?.Format,
                                        Description = response.Description,
                                        SchemaJson = TrySchemaToJson(media.Schema)
                                    });
                                }
                            }
                            else
                            {
                                ep.Responses.Add(new ApiTypeEndpointResponse
                                {
                                    StatusCode = status,
                                    ContentType = null,
                                    Type = null,
                                    Format = null,
                                    Description = response.Description
                                });
                            }
                        }
                    }

                    endpoints.Add(ep);
                }
            }

            _db.ApiTypeEndpoints.AddRange(endpoints);
            await _db.SaveChangesAsync(ct);
            return endpoints.Count;
        }

        private async Task<int> SaveFromGoogleDiscoveryAsync(int apiTypeId, JsonElement root, CancellationToken ct)
        {
            // Google Discovery: nested resources->methods, each method has httpMethod, path, parameters, response
            var endpoints = new List<ApiTypeEndpoint>();
            void Extract(JsonElement node)
            {
                if (node.TryGetProperty("methods", out var methods) && methods.ValueKind == JsonValueKind.Object)
                {
                    foreach (var m in methods.EnumerateObject())
                    {
                        var obj = m.Value;
                        var ep = new ApiTypeEndpoint
                        {
                            ApiTypeId = apiTypeId,
                            Method = obj.TryGetProperty("httpMethod", out var hm) ? hm.GetString() ?? "GET" : "GET",
                            Path = "/" + (obj.TryGetProperty("path", out var p) ? (p.GetString() ?? "").TrimStart('/') : ""),
                            OperationId = obj.TryGetProperty("id", out var id) ? id.GetString() : null,
                            Summary = obj.TryGetProperty("description", out var d) ? d.GetString() : null,
                            Description = obj.TryGetProperty("description", out var d2) ? d2.GetString() : null,
                            Deprecated = obj.TryGetProperty("deprecated", out var dep) && dep.ValueKind == JsonValueKind.True
                        };

                        if (obj.TryGetProperty("parameters", out var paramsObj) && paramsObj.ValueKind == JsonValueKind.Object)
                        {
                            foreach (var param in paramsObj.EnumerateObject())
                            {
                                var pobj = param.Value;
                                ep.Parameters.Add(new ApiTypeEndpointParameter
                                {
                                    Name = param.Name,
                                    In = pobj.TryGetProperty("location", out var loc) ? (loc.GetString() ?? "query") : "query",
                                    Required = pobj.TryGetProperty("required", out var req) && req.ValueKind == JsonValueKind.True,
                                    Type = pobj.TryGetProperty("type", out var t) ? t.GetString() : null,
                                    Format = pobj.TryGetProperty("format", out var f) ? f.GetString() : null,
                                    SchemaJson = pobj.GetRawText()
                                });
                            }
                        }

                        if (obj.TryGetProperty("response", out var resp) && resp.ValueKind == JsonValueKind.Object)
                        {
                            // Discovery “response” describes a single response schema
                            ep.Responses.Add(new ApiTypeEndpointResponse
                            {
                                StatusCode = "200",
                                ContentType = "application/json",
                                Type = resp.TryGetProperty("type", out var t) ? t.GetString() : null,
                                Description = ep.Summary,
                                SchemaJson = resp.GetRawText()
                            });
                        }

                        endpoints.Add(ep);
                    }
                }

                if (node.TryGetProperty("resources", out var res) && res.ValueKind == JsonValueKind.Object)
                {
                    foreach (var r in res.EnumerateObject())
                        Extract(r.Value);
                }
            }

            Extract(root);

            _db.ApiTypeEndpoints.AddRange(endpoints);
            await _db.SaveChangesAsync(ct);
            return endpoints.Count;
        }

        private static string? TrySchemaToJson(OpenApiSchema? schema)
        {
            if (schema == null) return null;
            try
            {
                // Minimal projection
                var minimal = new
                {
                    type = schema.Type,
                    format = schema.Format,
                    @ref = schema.Reference?.ReferenceV3,
                    items = schema.Items != null ? new { type = schema.Items.Type, format = schema.Items.Format } : null,
                    properties = schema.Properties?.ToDictionary(k => k.Key, v => new { type = v.Value.Type, format = v.Value.Format })
                };
                return JsonSerializer.Serialize(minimal);
            }
            catch { return null; }
        }
    }
}