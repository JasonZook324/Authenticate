using Microsoft.AspNetCore.Mvc.Rendering;

namespace Authenticate.Models
{
    public class ApiPlaygroundViewModel
    {
        // Inputs
        public int? SelectedCredentialId { get; set; }
        public List<SelectListItem> AvailableCredentials { get; set; } = new();
        // Map credential id -> API type name (e.g., ESPN) for UI behaviors
        public Dictionary<int, string> CredentialApiTypes { get; set; } = new();
        // Map credential id -> extras (defaults/placeholders)
        public Dictionary<int, CredentialExtras> CredentialExtras { get; set; } = new();

        public string? BaseUrl { get; set; }
        public string? EndpointPath { get; set; }
        public string? Query { get; set; }
        public string Method { get; set; } = "GET"; // GET, POST, PUT, PATCH, DELETE, HEAD, OPTIONS
        public string? BodyJson { get; set; }
        public string? HeadersRaw { get; set; } // One per line: Name: Value

        public bool UseCredentialAuth { get; set; } = true; // apply selected credential auth to request

        // Output
        public string? ResolvedUrl { get; set; }
        public int? StatusCode { get; set; }
        public long? ElapsedMs { get; set; }
        public string? ResponseHeaders { get; set; }
        public string? ResponseBody { get; set; }
        public string? ResponseBodyPretty { get; set; }
        public string? Error { get; set; }
    }

    public class CredentialExtras
    {
        public string? ApiName { get; set; }
        public string? BaseUrl { get; set; }
        public string? EspnSeasonId { get; set; }
        public string? EspnLeagueId { get; set; }
    }
}
