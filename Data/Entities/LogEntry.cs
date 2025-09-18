namespace Authenticate.Data.Entities
{
    public class LogEntry
    {
        public int Id { get; set; }
        public DateTimeOffset TimestampUtc { get; set; } = DateTimeOffset.UtcNow;

        public string Category { get; set; } = "";
        public string Level { get; set; } = "";          // e.g., Information, Warning, Error
        public int? EventId { get; set; }

        public string? Message { get; set; }
        public string? Exception { get; set; }

        // Helpful context
        public string? UserName { get; set; }
        public string? RequestPath { get; set; }
        public string? RequestId { get; set; }           // TraceIdentifier / correlation id
        public string? RemoteIp { get; set; }
    }
}