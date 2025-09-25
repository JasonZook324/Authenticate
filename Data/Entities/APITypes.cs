namespace Authenticate.Data.Entities
{
    public class APITypes
    {
        public int Id { get; set; } // Primary key
        public string ApiName { get; set; }
        public string? Description { get; set; }

        // New: where to fetch docs (OpenAPI/Swagger or Google Discovery)
        public string? DocumentationUrl { get; set; }
        public DateTime? LastSyncedUtc { get; set; }
    }
}
