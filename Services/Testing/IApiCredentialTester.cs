using System.Net;
using Authenticate.Data.Entities;

namespace Authenticate.Services.Testing;

public record ApiTestResult(bool Success, int? StatusCode, string? Url, string Message);

public interface IApiCredentialTester
{
    bool CanHandle(ApiCredential cred, string apiTypeName, Uri baseUri);
    Task<ApiTestResult> TestAsync(ApiCredential cred, Uri baseUri, CancellationToken ct = default);
}