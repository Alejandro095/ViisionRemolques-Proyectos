using System.ComponentModel.DataAnnotations;

namespace ViisionRemolques.Settings
{
    public class FDLibSettings
    {
        [Required(ErrorMessage = "El '{0}' en FDLibSettings es obligatorio.")]
        public string Nombre { get; set; } = string.Empty;

        [Required(ErrorMessage = "El '{0}' en FDLibSettings es obligatorio.")]
        [Range(1, 100, ErrorMessage = "El valor de '{0}' en FDLibSettings debe estar entre {1} y {2}.")]
        public int Threshold { get; set; }
    }
}
