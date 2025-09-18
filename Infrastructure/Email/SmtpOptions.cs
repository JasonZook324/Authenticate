namespace Authenticate.Infrastructure.Email
{
    public class SmtpOptions
    {
        public string Host { get; set; } = "";
        public int Port { get; set; } = 587;
        public bool UseStartTls { get; set; } = true;
        public string? User { get; set; }
        public string? Password { get; set; }
        public string From { get; set; } = "no-reply@example.com";
        public string FromName { get; set; } = "MyApp";
    }
}