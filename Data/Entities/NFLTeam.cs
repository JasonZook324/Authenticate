using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Authenticate.Data.Entities
{
    public class NFLTeam
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string TeamID { get; set; } = string.Empty;

        [Required]
        public string TeamAbv { get; set; } = string.Empty;

        [Required]
        public string TeamCity { get; set; } = string.Empty;

        [Required]
        public string TeamName { get; set; } = string.Empty;

        public string Division { get; set; } = string.Empty;
        public string ConferenceAbv { get; set; } = string.Empty;
        public string Conference { get; set; } = string.Empty;

        public string NflComLogo1 { get; set; } = string.Empty;
        public string EspnLogo1 { get; set; } = string.Empty;

        public string Loss { get; set; } = "0";
        public string Tie { get; set; } = "0";
        public string Wins { get; set; } = "0";
        public string Pa { get; set; } = "0";
        public string Pf { get; set; } = "0";

        // JSON serialized for simplicity
        public string CurrentStreakJson { get; set; } = "{}";
        public string ByeWeeksJson { get; set; } = "{}";
    }
}