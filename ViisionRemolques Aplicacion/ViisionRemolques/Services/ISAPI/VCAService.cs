using RestSharp;
using System.Xml.Linq;
using ViisionRemolques.Entities;

namespace ViisionRemolques.Services.ISAPI
{
    public class VCAService
    {
        private readonly ISAPIClientFactoryService _clientFactory;
        public VCAService(ISAPIClientFactoryService ISAPIClientFactoryService) {
            _clientFactory = ISAPIClientFactoryService;
        }

        public async Task<string> ObtenerModoActualAsync(Camara camara, CancellationToken ct = default)
        {
            using var client = _clientFactory.Crear(camara);

            var request = new RestRequest("/ISAPI/System/Video/inputs/channels/1/VCAResource", Method.Get);

            var response = await client.ExecuteAsync(request, ct);

            if (!response.IsSuccessful || string.IsNullOrWhiteSpace(response.Content))
            {
                throw new InvalidOperationException(
                    $"Error al consultar el modo VCA de la cámara ({camara.IP}). HTTP Status: {response.StatusCode}. Error: {response.ErrorMessage}");
            }

            var doc = XDocument.Parse(response.Content);

            string? typeValue = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "type")?.Value;

            if (string.IsNullOrWhiteSpace(typeValue))
            {
                throw new InvalidOperationException("La respuesta XML de la cámara no contiene el campo <type>.");
            }

            return typeValue;
        }

        public async Task CambiarModoAsync(Camara camara, string nuevoModo, bool reiniciarAlFinalizar = true, CancellationToken ct = default)
        {
            using var client = _clientFactory.Crear(camara);

            XNamespace ns = "http://www.hikvision.com/ver20/XMLSchema";
            var xmlPayload = new XDocument(
                new XDeclaration("1.0", "UTF-8", null),
                new XElement(ns + "VCAResource",
                    new XAttribute("version", "2.0"),
                    new XElement(ns + "type", nuevoModo)
                )
            );

            var request = new RestRequest("/ISAPI/System/Video/inputs/channels/1/VCAResource", Method.Put);
            request.AddHeader("Content-Type", "application/xml");
            request.AddStringBody(xmlPayload.ToString(), DataFormat.Xml);

            var response = await client.ExecuteAsync(request, ct);

            if (!response.IsSuccessful)
            {
                throw new InvalidOperationException(
                    $"Error al cambiar el modo VCA de la cámara ({camara.IP}) a '{nuevoModo}'. HTTP Status: {response.StatusCode}. Error: {response.ErrorMessage}. Respuesta: {response.Content}");
            }

            if (reiniciarAlFinalizar)
            {
                await ReiniciarAsync(camara, ct);
            }
        }

        public async Task ReiniciarAsync(Camara camara, CancellationToken ct = default)
        {
            using var client = _clientFactory.Crear(camara);

            var request = new RestRequest("/ISAPI/System/reboot", Method.Put);

            var response = await client.ExecuteAsync(request, ct);

            if (!response.IsSuccessful)
            {
                throw new InvalidOperationException(
                    $"Error al solicitar el reinicio de la cámara ({camara.IP}). HTTP Status: {response.StatusCode}. Error: {response.ErrorMessage}. Respuesta: {response.Content}");
            }
        }

    }
}
