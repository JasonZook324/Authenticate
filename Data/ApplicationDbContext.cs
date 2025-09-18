using Microsoft.EntityFrameworkCore;
using Authenticate.Data.Entities;

namespace Authenticate.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<Email> Emails { get; set; }
        public DbSet<Password> Passwords { get; set; }
        public DbSet<Menu> Menus { get; set; }
        public DbSet<LogEntry> Logs { get; set; }
        public DbSet<PasswordReset> PasswordResets { get; set; } // NEW
    }
}