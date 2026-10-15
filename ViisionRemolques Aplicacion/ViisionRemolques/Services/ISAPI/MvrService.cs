using Microsoft.Extensions.Options;
using RestSharp;
using System.Xml.Linq;
using ViisionRemolques.Settings;
using ViisionRemolques.Utils;

namespace ViisionRemolques.Services.ISAPI
{
    /// <summary>
    /// Consultas de solo lectura al MVR (fuera del proxy de video), p.ej. verificar que exista
    /// grabación antes de emitir un token de clip.
    /// </summary>
    public class MvrService
    {
        private readonly IsapiClientFactoryService _isapiClientFactoryService;
        private readonly MvrSettings _mvrSettings;

        public MvrService(IsapiClientFactoryService isapiClientFactoryService, IOptions<MvrSettings> mvrSettings)
        {
            _isapiClientFactoryService = isapiClientFactoryService;
            _mvrSettings = mvrSettings.Value;
        }

        public async Task<Resultado<bool>> ExisteGrabacionAsync(int canal, DateTimeOffset inicio, DateTimeOffset fin, CancellationToken ct = default)
        {
            try
            {
                // Pista de video principal del canal: id*100+1 (convención ISAPI de Hikvision).
                var trackId = canal * 100 + 1;

                using var client = _isapiClientFactoryService.Crear(_mvrSettings.IP, _mvrSettings.Usuario, _mvrSettings.Secreto);

                // Este firmware rechaza el body en JSON ("Invalid XML Format") aunque otros
                // endpoints ISAPI lo acepten con ?format=json; /ContentMgmt/search exige XML puro.
                var xmlPayload = new XDocument(
                    new XDeclaration("1.0", "UTF-8", null),
                    new XElement("CMSearchDescription",
                        new XElement("searchID", Guid.NewGuid().ToString()),
                        new XElement("trackList",
                            new XElement("trackID", trackId)
                        ),
                        new XElement("timeSpanList",
                            new XElement("timeSpan",
                                new XElement("startTime", inicio.ToString("yyyy-MM-ddTHH:mm:ssK")),
                                new XElement("endTime", fin.ToString("yyyy-MM-ddTHH:mm:ssK"))
                            )
                        ),
                        new XElement("maxResults", 1),
                        new XElement("searchResultPostion", 0)
                    )
                );

                var request = new RestRequest("/ISAPI/ContentMgmt/search", Method.Post);
                request.AddHeader("Content-Type", "application/xml");
                request.AddStringBody(xmlPayload.ToString(), DataFormat.Xml);

                var response = await client.ExecuteAsync(request, ct);

                if (!response.IsSuccessful || string.IsNullOrWhiteSpace(response.Content))
                    return Resultado<bool>.Fallo($"Respuesta HTTP inválida o vacía al consultar grabaciones del MVR ({response.StatusCode}).");

                var doc = XDocument.Parse(response.Content);
                var numOfMatchesStr = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "numOfMatches")?.Value;

                var numOfMatches = int.TryParse(numOfMatchesStr, out var n) ? n : 0;

                return Resultado<bool>.Ok(numOfMatches > 0);
            }
            catch (Exception ex)
            {
                return Resultado<bool>.Fallo(ex);
            }
        }
    }
}
