using Microsoft.AspNetCore.Mvc.Rendering;

namespace Authenticate.Models
{
    public class RosterViewModel
    {
        public List<SelectListItem> Teams { get; set; } = new();
        public int? TeamId { get; set; }
        public string? SelectedTeamName { get; set; }
        public List<RosterEntry> RosterEntries { get; set; } = new();
        public string? Error { get; set; }
    }

    public class RosterEntry
    {
        public string Slot { get; set; } = string.Empty;
        public string PlayerName { get; set; } = string.Empty;
    }
}