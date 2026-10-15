using System.ComponentModel.DataAnnotations;

namespace ViisionRemolques.Settings
{
    public class JwtSettings
    {
        [Required(ErrorMessage = "El '{0}' en JwtSettings es obligatorio.")]
        public string Key { get; set; } = string.Empty;

        [Required(ErrorMessage = "El '{0}' en JwtSettings es obligatorio.")]
        public string Issuer { get; set; } = string.Empty;

        [Required(ErrorMessage = "El '{0}' en JwtSettings es obligatorio.")]
        public string Audience { get; set; } = string.Empty;
    }
}
