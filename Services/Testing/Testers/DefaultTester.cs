using System.Net;
using Authenticate.Data.Entities;
using Microsoft.AspNetCore.DataProtection;

namespace Authenticate.Services.Testing;

public class DefaultTester : TesterBase
{
    public DefaultTester(IHttpClientFactory http, IDataProtectionProvider dp) : base(http, dp) { }

    public override bool CanHandle(ApiCredential cred, string apiTypeName, Uri baseUri)
        => true; // fallback for unknown APIs

    public override async Task<ApiTestResult> TestAsync(ApiCredential cred, Uri baseUri, CancellationToken ct = default)
    {
        var isAzureOpenAI = baseUri.Host.Contains(".openai.azure.com", StringComparison.OrdinalIgnoreCase);
        var apiVersion = "2024-02-15-preview";

        var candidates = isAzureOpenAI
            ? new[]
            {
                $"{baseUri}/openai/models?api-version={apiVersion}",
                $"{baseUri}/openai/deployments?api-version={apiVersion}",
                $"{baseUri}",
                $"{baseUri}/health",
                $"{baseUri}/status",
            }
            : new[]
            {
                $"{baseUri}/v1/models",
                $"{baseUri}",
                $"{baseUri}/health",
                $"{baseUri}/status",
            };

        using var client = CreateJsonClient(Http, TimeSpan.FromSeconds(10));
        ApplyAuth(client, cred, isAzureOpenAI);

        HttpStatusCode? last = null;
        string? lastUrl = null;

        foreach (var url in candidates)
        {
            lastUrl = url;
            using var resp = await client.GetAsync(url, ct);
            last = resp.StatusCode;

            if ((int)last >= 200 && (int)last < 300)
                return new ApiTestResult(true, (int)last, url, "OK");

            if (last is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                return new ApiTestResult(false, (int)last, url, "Authentication failed");
        }

        return new ApiTestResult(false, (int?)last, lastUrl, "No candidate endpoint succeeded");
    }
}