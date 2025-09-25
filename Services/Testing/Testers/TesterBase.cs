using System.Net.Http.Headers;
using System.Text;
using Authenticate.Data.Entities;
using Microsoft.AspNetCore.DataProtection;

namespace Authenticate.Services.Testing;

public abstract class TesterBase : IApiCredentialTester
{
    protected readonly IHttpClientFactory Http;
    protected readonly IDataProtector Protector;

    protected TesterBase(IHttpClientFactory http, IDataProtectionProvider dp)
    {
        Http = http;
        Protector = dp.CreateProtector("ApiCredentials.v1");
    }

    public abstract bool CanHandle(ApiCredential cred, string apiTypeName, Uri baseUri);
    public abstract Task<ApiTestResult> TestAsync(ApiCredential cred, Uri baseUri, CancellationToken ct = default);

    protected string? Decrypt(string? cipher)
    {
        if (string.IsNullOrWhiteSpace(cipher)) return null;
        try { return Protector.Unprotect(cipher); } catch { return null; }
    }

    protected void ApplyAuth(HttpClient client, ApiCredential entity, bool isAzureOpenAI)
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

    protected static HttpClient CreateJsonClient(IHttpClientFactory http, TimeSpan timeout)
    {
        var c = http.CreateClient();
        c.Timeout = timeout;
        c.DefaultRequestHeaders.Accept.Clear();
        c.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        c.DefaultRequestHeaders.UserAgent.ParseAdd("AuthenticateApp/1.0");
        return c;
    }
}