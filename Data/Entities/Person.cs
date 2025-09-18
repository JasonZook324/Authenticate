using System.Collections.Generic;

namespace Authenticate.Data.Entities
{
    public class Person
    {
        public int Id { get; set; } // Primary key
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public int UserId { get; set; } // Foreign key
        public ICollection<User> Users { get; set; } // Navigation property
        
    }
}
