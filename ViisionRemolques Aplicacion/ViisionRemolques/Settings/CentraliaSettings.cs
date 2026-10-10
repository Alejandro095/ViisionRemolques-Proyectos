using System.Buffers.Text;

namespace ViisionRemolques.Settings
{
    public class CentraliaSettings
    {
        public string Dominio { get; set; } = string.Empty;

        public string ApiUsuario { get; set; } = string.Empty;
        public string ContraseniaUsuario { get; set; } = string.Empty;


        public string GetApiFullUrl(string endpoint) => 
            $"{endpoint.TrimEnd('/')}/{endpoint.TrimStart('/')}";
    }
}
