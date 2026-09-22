using RestSharp;
using RestSharp.Authenticators.Digest;
using ViisionRemolques.Entities;

namespace ViisionRemolques.Services
{
    public class ISAPIClientFactoryService
    {

        public RestClient Crear(Camara camara)
        {
            if (string.IsNullOrWhiteSpace(camara.IP))
                throw new InvalidOperationException($"La cámara '{camara.Nombre}' no tiene asignada una IP.");

            if (string.IsNullOrWhiteSpace(camara.DigestUsuario) || string.IsNullOrWhiteSpace(camara.DigestContrasena))
                throw new InvalidOperationException($"La cámara '{camara.Nombre}' no tiene configuradas credenciales Digest.");

            var baseUrl = $"http://{camara.IP}";

            var options = new RestClientOptions(baseUrl)
            {
                Timeout = TimeSpan.FromSeconds(15),
                Authenticator = new DigestAuthenticator(camara.DigestUsuario, camara.DigestContrasena)
            };
            return new RestClient(options);
        }

    }
}
