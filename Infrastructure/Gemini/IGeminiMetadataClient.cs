using System.Threading;
using System.Threading.Tasks;

namespace Authenticate.Infrastructure.Gemini
{
    public record GeminiEndpointDto(string Id, string HttpMethod, string Path, string? Description);
    public record GeminiModelDto(string Name, string? DisplayName);

    public interface IGeminiMetadataClient
    {
        Task<IReadOnlyList<GeminiEndpointDto>> GetEndpointsAsync(CancellationToken ct = default);
        Task<IReadOnlyList<GeminiModelDto>> GetModelsAsync(CancellationToken ct = default);
    }
}