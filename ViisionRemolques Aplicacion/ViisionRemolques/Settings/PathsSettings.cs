using System.ComponentModel.DataAnnotations;

namespace ViisionRemolques.Settings
{
    public class PathsSettings
    {
        [Required(ErrorMessage = "El '{0}' en CentraliaSettings es obligatorio.")]
        public string Imagenes { get; set; } = string.Empty;

        [Required(ErrorMessage = "El '{0}' en CentraliaSettings es obligatorio.")]
        public string Rostros { get; set; } = string.Empty;
    }
}
