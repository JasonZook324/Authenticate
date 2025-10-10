using Microsoft.AspNetCore.Mvc.Rendering;

namespace Authenticate.Models
{
    public class EspnApiPlaygroundViewModel
    {
        // Inputs
        public int? SelectedCredentialId { get; set; }
        public List<SelectListItem> AvailableCredentials { get; set; } = new();

        public string? BaseUrl { get; set; } = "https://lm-api-reads.fantasy.espn.com";
        public string? SeasonId { get; set; }
        public string? LeagueId { get; set; }
        public string? EndpointPath { get; set; }
        public string? Query { get; set; }
        public string? Method { get; set; } = "GET";
        public string? BodyJson { get; set; }

        // Cookies
        public bool UseCredentialCookies { get; set; } = true;
        public string? SWID { get; set; }
        public string? EspnS2 { get; set; }

        // Output
        public string? ResolvedUrl { get; set; }
        public int? StatusCode { get; set; }
        public long? ElapsedMs { get; set; }
        public string? ResponseHeaders { get; set; }
        public string? ResponseBody { get; set; }
        public string? ResponseBodyPretty { get; set; }
        public string? ResponseJSON { get; set; }

        public string? Error { get; set; }
    }
}