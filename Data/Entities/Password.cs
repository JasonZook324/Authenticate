namespace Authenticate.Data.Entities
{
    public class Password
    {
        public int Id { get; set; } // Primary key
        public string? PasswordHash { get; set; }
        public int UserId { get; set; } // Foreign key to User
        public User? User { get; set; } // Navigation property
    }
}
