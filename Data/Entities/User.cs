using System.Collections.Generic;

namespace Authenticate.Data.Entities
{
    public class User
    {
        public int Id { get; set; } // Primary key
        public string Username { get; set; }
        public bool IsActive { get; set; }
        public bool IsAdmin { get; set; }
        public ICollection<Email> Emails { get; set; } // Navigation property
        public ICollection<Password> Passwords { get; set; } // Navigation property
        public ICollection<Person> Persons { get; set; } // Navigation property
    }
}
