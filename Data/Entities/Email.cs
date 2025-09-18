namespace Authenticate.Data.Entities
{
    public class Email
    {
        public int Id { get; set; } // Primary key
        public string? EmailAddress { get; set; }
        public bool IsVerified { get; set; }
        public User? User { get; set; } // Navigation property
    }
}
