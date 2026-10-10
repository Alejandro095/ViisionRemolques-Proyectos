using System.ComponentModel.DataAnnotations;

namespace ViisionRemolques.Settings
{
    public class CentraliaSettings
    {
        [Required(ErrorMessage = "El '{0}' en CentraliaSettings es obligatorio.")]
        public string Dominio { get; set; } = string.Empty;
        [Required(ErrorMessage = "El '{0}' en CentraliaSettings es obligatorio.")]
        public string Licencia { get; set; } = string.Empty;
        [Required(ErrorMessage = "El '{0}' en CentraliaSettings es obligatorio.")]
        public string Usuario { get; set; } = string.Empty;
        [Required(ErrorMessage = "El '{0}' en CentraliaSettings es obligatorio.")]
        public string Secreto { get; set; } = string.Empty;

        public string GetApiFullUrl(string endpoint) => 
            $"{Dominio.TrimEnd('/')}/{endpoint.TrimStart('/')}";
    }
}
