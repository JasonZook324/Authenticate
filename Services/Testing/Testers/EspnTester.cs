using Authenticate.Data.Entities;
using Microsoft.AspNetCore.DataProtection;
namespace Authenticate.Services.Testing;

public class EspnTester : TesterBase
{
    public EspnTester(IHttpClientFactory http, IDataProtectionProvider dp) : base(http, dp) { }

    public override bool CanHandle(ApiCredential cred, string apiTypeName, Uri baseUri)
        => apiTypeName.Contains("ESPN", StringComparison.OrdinalIgnoreCase)
           || baseUri.Host.Contains("espn.com", StringComparison.OrdinalIgnoreCase);

    public override async Task<ApiTestResult> TestAsync(ApiCredential cred, Uri baseUri, CancellationToken ct = default)
    {
        // Use saved values; require them for ESPN paths
        var seasonId = string.IsNullOrWhiteSpace(cred.EspnSeasonId) ? null : cred.EspnSeasonId!.Trim();
        var leagueId = string.IsNullOrWhiteSpace(cred.EspnLeagueId) ? null : cred.EspnLeagueId!.Trim();

        var paths = new List<string>();
        if (!string.IsNullOrWhiteSpace(seasonId) && !string.IsNullOrWhiteSpace(leagueId))
        {
            paths.Add($"/apis/v3/games/ffl/seasons/{seasonId}/segments/0/leagues/{leagueId}");
        }
        // Fallbacks for availability checks
        paths.Add("/apis/site/v2/sports");
        paths.Add("/v2/sports");

        using var client = CreateJsonClient(Http, TimeSpan.FromSeconds(10));
        ApplyAuth(client, cred, isAzureOpenAI: false);

        foreach (var p in paths)
        {
            var url = new Uri(baseUri, p).ToString();
            using var resp = await client.GetAsync(url, ct);
            var code = (int)resp.StatusCode;
            if (code is >= 200 and < 300)
                return new ApiTestResult(true, code, url, "OK");
            if (code is 401 or 403)
                return new ApiTestResult(false, code, url, "Authentication failed");
        }

        var msg = (seasonId == null || leagueId == null)
            ? "Set ESPN Season Id and League Id to fully validate the credential."
            : "No ESPN endpoint responded OK";
        return new ApiTestResult(false, null, baseUri.ToString(), msg);
    }
}