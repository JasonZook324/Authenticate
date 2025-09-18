using System;

namespace Authenticate.Data.Entities
{
    public class PasswordReset
    {
        public int Id { get; set; }
        public string Token { get; set; } = string.Empty; // URL-safe token
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime ExpiresAtUtc { get; set; }
        public DateTime? UsedAtUtc { get; set; }

        public int UserId { get; set; }
        public User? User { get; set; }
    }
}