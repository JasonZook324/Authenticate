using System.Threading;
using System.Threading.Tasks;

namespace Authenticate.Infrastructure.Email
{
    public interface IEmailSender
    {
        Task SendAsync(string toEmail, string subject, string? htmlBody, string? textBody = null, CancellationToken ct = default);
    }
}