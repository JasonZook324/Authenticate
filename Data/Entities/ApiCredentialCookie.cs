using System.ComponentModel.DataAnnotations;

namespace Authenticate.Data.Entities
{
    public class ApiCredentialCookie
    {
        public int Id { get; set; }

        [Required]
        public int ApiCredentialId { get; set; }
        public ApiCredential? ApiCredential { get; set; }

        [Required, MaxLength(128)]
        public string Name { get; set; } = string.Empty;

        // Encrypted ciphertext (never store plaintext)
        [Required]
        public string EncryptedValue { get; set; } = string.Empty;
    }
}