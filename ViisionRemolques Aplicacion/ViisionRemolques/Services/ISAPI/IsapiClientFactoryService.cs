using RestSharp;
using RestSharp.Authenticators.Digest;
using System;
using System.Net.Http;
using ViisionRemolques.Repositories;

namespace ViisionRemolques.Services.ISAPI
{
    public class IsapiClientFactoryService
    {
        private readonly IHttpClientFactory _httpClientFactory;

        public IsapiClientFactoryService(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        public RestClient Crear(CamaraEntity camara)
        {
            if (string.IsNullOrWhiteSpace(camara.IP))
                throw new InvalidOperationException($"La cámara '{camara.Nombre}' no tiene asignada una IP.");

            if (string.IsNullOrWhiteSpace(camara.DigestUsuario) || string.IsNullOrWhiteSpace(camara.DigestContrasena))
                throw new InvalidOperationException($"La cámara '{camara.Nombre}' no tiene configuradas credenciales Digest.");

            var baseUrl = $"http://{camara.IP}";

            var httpClient = _httpClientFactory.CreateClient("IsapiCameraClient");

            httpClient.Timeout = TimeSpan.FromSeconds(120);

            return new RestClient(httpClient, new RestClientOptions(baseUrl)
            {
                Authenticator = new DigestAuthenticator(camara.DigestUsuario, camara.DigestContrasena)
            });
        }
    }
}