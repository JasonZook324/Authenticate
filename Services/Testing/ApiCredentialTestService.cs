using Authenticate.Data.Entities;

namespace Authenticate.Services.Testing;

public class ApiCredentialTestService
{
    private readonly IEnumerable<IApiCredentialTester> _testers;

    public ApiCredentialTestService(IEnumerable<IApiCredentialTester> testers)
        => _testers = testers;

    public IApiCredentialTester Resolve(ApiCredential cred, string apiTypeName, Uri baseUri)
        => _testers.FirstOrDefault(t => t.CanHandle(cred, apiTypeName, baseUri))
           ?? _testers.First(t => t is DefaultTester);
}