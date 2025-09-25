using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Authenticate.Data.Entities
{
    public enum ApiAuthType
    {
        [Display(Name = "Token Authentication (Authorization: Bearer)")]
        BearerToken = 0,
        [Display(Name = "Cookie Authentication")]
        Cookie = 1,
        [Display(Name = "User/Pass (Basic)")]
        Basic = 2
    }

    public class ApiCredential
    {
        public int Id { get; set; }

        [Required, MaxLength(128)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(512)]
        public string? BaseUrl { get; set; }

        [Required]
        public ApiAuthType AuthType { get; set; } = ApiAuthType.BearerToken;

        // Token/Bearer
        public string? EncryptedApiKey { get; set; } = null;

        [MaxLength(64)]
        public string? TokenHeaderName { get; set; } = "Authorization";

        [MaxLength(32)]
        public string? TokenScheme { get; set; } = "Bearer";

        // Deprecated single-cookie fields (kept for backward compat and migration)
        [MaxLength(128)]
        public string? CookieName { get; set; }
        public string? EncryptedCookieValue { get; set; }

        // Basic auth
        public string? EncryptedUsername { get; set; }
        public string? EncryptedPassword { get; set; }

        // Multi-cookie support
        public ICollection<ApiCredentialCookie> Cookies { get; set; } = new List<ApiCredentialCookie>();

        // ESPN-specific optional fields
        [MaxLength(32)]
        public string? EspnSeasonId { get; set; }

        [MaxLength(32)]
        public string? EspnLeagueId { get; set; }

        [Required]
        public int ApiTypeId { get; set; }
        public APITypes? ApiType { get; set; }

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

        public bool IsVerified { get; set; } = false;
        public DateTime? VerifiedAtUtc { get; set; }

        public int UserId { get; set; }
        public User? User { get; set; }
    }
}