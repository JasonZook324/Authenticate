using System.Threading;
using System.Threading.Tasks;

namespace Authenticate.Infrastructure.ApiDocs
{
    public interface IApiDocIngestionService
    {
        Task<int> IngestAsync(int apiTypeId, string documentationUrl, CancellationToken ct = default);
        Task<int> RefreshAsync(int apiTypeId, CancellationToken ct = default);
    }
}