using System.Threading;
using System.Threading.Tasks;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Authenticate.Infrastructure.Email
{
    public class SmtpEmailSender(IOptions<SmtpOptions> options) : IEmailSender
    {
        private readonly SmtpOptions _opt = options.Value;

        public async Task SendAsync(string toEmail, string subject, string? htmlBody, string? textBody = null, CancellationToken ct = default)
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_opt.FromName, _opt.From));
            message.To.Add(MailboxAddress.Parse(toEmail));
            message.Subject = subject;

            var bodyBuilder = new BodyBuilder
            {
                HtmlBody = htmlBody,
                TextBody = textBody ?? StripHtml(htmlBody) ?? ""
            };
            message.Body = bodyBuilder.ToMessageBody();

            using var client = new SmtpClient();
            var secure = _opt.UseStartTls ? SecureSocketOptions.StartTls : SecureSocketOptions.Auto;
            await client.ConnectAsync(_opt.Host, _opt.Port, secure, ct);

            if (!string.IsNullOrWhiteSpace(_opt.User))
            {
                await client.AuthenticateAsync(_opt.User, _opt.Password, ct);
            }

            await client.SendAsync(message, ct);
            await client.DisconnectAsync(true, ct);
        }

        private static string? StripHtml(string? html)
            => string.IsNullOrWhiteSpace(html) ? html : System.Text.RegularExpressions.Regex.Replace(html, "<.*?>", string.Empty);
    }
}