using System.ComponentModel.DataAnnotations;

namespace Authenticate.Data.Entities
{
    public class ApiTypeEndpoint
    {
        public int Id { get; set; }

        public int ApiTypeId { get; set; }
        public APITypes? ApiType { get; set; }

        [Required, MaxLength(12)]
        public string Method { get; set; } = "GET";

        [Required, MaxLength(512)]
        public string Path { get; set; } = "/";

        [MaxLength(256)]
        public string? OperationId { get; set; }

        [MaxLength(512)]
        public string? Summary { get; set; }

        public string? Description { get; set; }

        public bool Deprecated { get; set; }

        [MaxLength(512)]
        public string? ExternalDocsUrl { get; set; }

        public ICollection<ApiTypeEndpointParameter> Parameters { get; set; } = new List<ApiTypeEndpointParameter>();
        public ICollection<ApiTypeEndpointResponse> Responses { get; set; } = new List<ApiTypeEndpointResponse>();
    }

    public class ApiTypeEndpointParameter
    {
        public int Id { get; set; }

        public int EndpointId { get; set; }
        public ApiTypeEndpoint? Endpoint { get; set; }

        [Required, MaxLength(128)]
        public string Name { get; set; } = "";

        // path, query, header, cookie, body
        [Required, MaxLength(16)]
        public string In { get; set; } = "query";

        public bool Required { get; set; }

        [MaxLength(64)]
        public string? Type { get; set; }

        [MaxLength(64)]
        public string? Format { get; set; }

        // Optional raw schema JSON (if available)
        public string? SchemaJson { get; set; }
    }

    public class ApiTypeEndpointResponse
    {
        public int Id { get; set; }

        public int EndpointId { get; set; }
        public ApiTypeEndpoint? Endpoint { get; set; }

        [MaxLength(16)]
        public string? StatusCode { get; set; }

        [MaxLength(128)]
        public string? ContentType { get; set; }

        // e.g., object/array/string/...
        [MaxLength(64)]
        public string? Type { get; set; }

        [MaxLength(64)]
        public string? Format { get; set; }

        public string? Description { get; set; }

        public string? SchemaJson { get; set; }
    }
}