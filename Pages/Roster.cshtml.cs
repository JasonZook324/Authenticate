using Microsoft.AspNetCore.Mvc.RazorPages;
using Authenticate.Data;
using Authenticate.Data.Entities;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Authenticate.Pages
{
    public class RosterModel : PageModel
    {
        private readonly ApplicationDbContext _db;

        public RosterModel(ApplicationDbContext db)
        {
            _db = db;
        }

        public List<NFLTeam> Teams { get; set; } = new();

        public async Task OnGetAsync()
        {
            Teams = await _db.NFLTeams.ToListAsync();
        }
    }
}