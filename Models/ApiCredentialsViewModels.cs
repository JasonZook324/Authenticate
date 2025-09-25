using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Collections.Generic;
using Authenticate.Data.Entities;

namespace Authenticate.Models
{
    public class CookiePairInput
    {
        [MaxLength(128)]
        public string? Name { get; set; }
        public string? Value { get; set; }
    }

    public class ApiCredentialCreateModel
    {
        [Required, MaxLength(128)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(512)]
        public string? BaseUrl { get; set; }

        [Required]
        [Display(Name = "Authentication")]
        public ApiAuthType AuthType { get; set; } = ApiAuthType.BearerToken;

        // Token/Bearer (optional unless AuthType == BearerToken)
        [Display(Name = "Token/Key")]
        public string? ApiKey { get; set; }

        [MaxLength(64)]
        [Display(Name = "Token Header Name")]
        public string TokenHeaderName { get; set; } = "Authorization";

        [MaxLength(32)]
        [Display(Name = "Token Scheme")]
        public string TokenScheme { get; set; } = "Bearer";

        // Cookie (multiple)
        public List<CookiePairInput> CookiePairs { get; set; } = new();

        // Basic
        [Display(Name = "Username")]
        public string? Username { get; set; }

        [Display(Name = "Password")]
        public string? Password { get; set; }

        // ESPN
        [MaxLength(32)]
        [Display(Name = "ESPN Season Id")]
        public string? EspnSeasonId { get; set; }

        [MaxLength(32)]
        [Display(Name = "ESPN League Id")]
        public string? EspnLeagueId { get; set; }

        [Required]
        [Display(Name = "API Type")]
        public int ApiTypeId { get; set; }

        public IEnumerable<SelectListItem> ApiTypeOptions { get; set; } = new List<SelectListItem>();
    }

    public class ApiCredentialEditModel
    {
        [Required]
        public int Id { get; set; }

        [Required, MaxLength(128)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(512)]
        public string? BaseUrl { get; set; }

        [Required]
        [Display(Name = "Authentication")]
        public ApiAuthType AuthType { get; set; } = ApiAuthType.BearerToken;

        // Token/Bearer
        [MinLength(10)]
        [Display(Name = "New Token/Key (leave blank to keep current)")]
        public string? NewApiKey { get; set; }

        [MaxLength(64)]
        [Display(Name = "Token Header Name")]
        public string TokenHeaderName { get; set; } = "Authorization";

        [MaxLength(32)]
        [Display(Name = "Token Scheme")]
        public string TokenScheme { get; set; } = "Bearer";

        // Cookie (multiple) - submitting non-empty pairs will replace the saved set
        public List<CookiePairInput> CookiePairs { get; set; } = new();

        // Basic
        [Display(Name = "Username")]
        public string? Username { get; set; }

        [Display(Name = "New Password (leave blank to keep current)")]
        public string? NewPassword { get; set; }

        // ESPN
        [MaxLength(32)]
        [Display(Name = "ESPN Season Id")]
        public string? EspnSeasonId { get; set; }

        [MaxLength(32)]
        [Display(Name = "ESPN League Id")]
        public string? EspnLeagueId { get; set; }

        [Required]
        [Display(Name = "API Type")]
        public int ApiTypeId { get; set; }

        public IEnumerable<SelectListItem> ApiTypeOptions { get; set; } = new List<SelectListItem>();

        public string MaskedKey { get; set; } = "••••";

        public bool IsVerified { get; set; }

        public string ApiTypeName { get; set; } = string.Empty;
        public string? ApiTypeDescription { get; set; }
    }
}