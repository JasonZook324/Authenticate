using Authenticate.Data.Entities;
using Microsoft.AspNetCore.DataProtection;
namespace Authenticate.Services.Testing;

public class GeminiTester : TesterBase
{
    public GeminiTester(IHttpClientFactory http, IDataProtectionProvider dp) : base(http, dp) { }

    public override bool CanHandle(ApiCredential cred, string apiTypeName, Uri baseUri)
        => apiTypeName.Contains("Gemini", StringComparison.OrdinalIgnoreCase)
           || baseUri.Host.Contains("generativelanguage.googleapis.com", StringComparison.OrdinalIgnoreCase);

    public override async Task<ApiTestResult> TestAsync(ApiCredential cred, Uri baseUri, CancellationToken ct = default)
    {
        // Public model listing; many deployments use v1beta
        var candidates = new[]
        {
            new Uri(baseUri, "/v1beta/models").ToString(),
            new Uri(baseUri, "/v1/models").ToString()
        };

        using var client = CreateJsonClient(Http, TimeSpan.FromSeconds(10));
        ApplyAuth(client, cred, isAzureOpenAI: false);

        foreach (var url in candidates)
        {
            using var resp = await client.GetAsync(url, ct);
            var code = (int)resp.StatusCode;
            if (code is >= 200 and < 300)
                return new ApiTestResult(true, code, url, "OK");
            if (code is 401 or 403)
                return new ApiTestResult(false, code, url, "Authentication failed");
        }

        return new ApiTestResult(false, null, baseUri.ToString(), "No Gemini endpoint responded OK");
    }
}