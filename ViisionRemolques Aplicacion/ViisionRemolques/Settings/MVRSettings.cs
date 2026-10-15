using System.ComponentModel.DataAnnotations;

namespace ViisionRemolques.Settings
{
    public class MvrSettings
    {
        [Required(ErrorMessage = "El '{0}' en MVRSettings es obligatorio.")]
        public string IP { get; set; } = string.Empty;

        [Required(ErrorMessage = "El '{0}' en MVRSettings es obligatorio.")]
        public string Usuario { get; set; } = string.Empty;

        [Required(ErrorMessage = "El '{0}' en MVRSettings es obligatorio.")]
        public string Secreto { get; set; } = string.Empty;

        public int RtspPort { get; set; } = 554;

        public int ClipPreSegundos { get; set; } = 10;
        public int ClipPostSegundos { get; set; } = 15;
    }
}
