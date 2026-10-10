using RestSharp;
using System.Net.NetworkInformation;
using System.Xml.Linq;
using ViisionRemolques.Enums;
using ViisionRemolques.Repositories;
using ViisionRemolques.Utils;

namespace ViisionRemolques.Services.ISAPI.VCA.Providers
{
    public class LegacyIsapiVcaProvider : IVcaProvider
    {
        private readonly IsapiClientFactoryService _isapiClientFactoryService;

        bool IVcaProvider.RequiereReinicioAlCambiarModo => true;

        private static readonly Dictionary<VCAModoEnum, string> TypePorModo = new()
        {
            [VCAModoEnum.ArmadoPistaPersona] = "personArming",
            [VCAModoEnum.DeteccionTipoMultiObjetivo] = "mixedTargetDetection",
            [VCAModoEnum.DeteccionTipoMultiObjetivoComparacion] = "faceHumanModelingContrast",
            [VCAModoEnum.EventoSmart] = "smart",
            [VCAModoEnum.Monitorizacion] = "close",
            [VCAModoEnum.TraficoRodado] = "roadDetection",
        };

        public LegacyIsapiVcaProvider(IsapiClientFactoryService isapiClientFactoryService)
        {
            _isapiClientFactoryService = isapiClientFactoryService;
        }
        private static readonly Dictionary<string, VCAModoEnum> ModoPorType =
           TypePorModo.ToDictionary(kv => kv.Value, kv => kv.Key, StringComparer.OrdinalIgnoreCase);

        public async Task<Resultado<IEnumerable<VCAModoEnum>>> ObtenerModosSoportados(CamaraEntity camara, CancellationToken ct)
        {
            try
            {
                using var client = _isapiClientFactoryService.Crear(camara);

                var response = await client.ExecuteAsync(new RestRequest(
                   "/ISAPI/System/Video/inputs/channels/1/VCAResource/capabilities",
                   Method.Get
               ), ct);

                if (!response.IsSuccessful || string.IsNullOrWhiteSpace(response.Content))
                    return Resultado<IEnumerable<VCAModoEnum>>.Fallo(
                        $"Error al consultar capabilities VCA de la cámara ({camara.IP}). HTTP {response.StatusCode}.");

                var doc = XDocument.Parse(response.Content);
                var opt = doc.Descendants()
                    .FirstOrDefault(e => e.Name.LocalName == "type")
                    ?.Attribute("opt")?.Value;

                if (string.IsNullOrWhiteSpace(opt))
                    return Resultado<IEnumerable<VCAModoEnum>>.Fallo("La respuesta no contiene <type opt=\"...\">.");

                var modos = opt.Split(',')
                    .Select(t => t.Trim())
                    .Where(ModoPorType.ContainsKey)
                    .Select(t => ModoPorType[t]);

                return Resultado<IEnumerable<VCAModoEnum>>.Ok(modos);
            } catch
            {
                return Resultado<IEnumerable<VCAModoEnum>>.Fallo($"Excepción al consultar capabilities VCA ({camara.IP})");
            }
        }

        async Task<Resultado> IVcaProvider.CambiarModo(CamaraEntity camara, VCAModoEnum modo, CancellationToken ct)
        {
            try
            {
                if (!TypePorModo.TryGetValue(modo, out var vcaModo))
                    return Resultado.Fallo("El VCA no ha sido registrado en el catalogo de modos.");

                using var client = _isapiClientFactoryService.Crear(camara);

                XNamespace ns = "http://www.hikvision.com/ver20/XMLSchema";
                var xmlPayload = new XDocument(
                    new XDeclaration("1.0", "UTF-8", null),
                    new XElement(ns + "VCAResource",
                        new XAttribute("version", "2.0"),
                        new XElement(ns + "type", vcaModo)
                    )
                );

                var request = new RestRequest("/ISAPI/System/Video/inputs/channels/1/VCAResource", Method.Put);
                request.AddHeader("Content-Type", "application/xml");
                request.AddStringBody(xmlPayload.ToString(), DataFormat.Xml);
                var response = await client.ExecuteAsync(request, ct);

                if (!response.IsSuccessStatusCode)
                    return Resultado.Fallo($"Error al cambiar el modo VCA de la cámara ({camara.IP})");


                return Resultado.Ok();
            } catch
            {
                return Resultado.Fallo($"Excepción al cambiar el modo VCA ({camara.IP})");
            }
        }

        async Task<Resultado<VCAModoEnum>> IVcaProvider.ObtenerModoActual(CamaraEntity camara, CancellationToken ct)
        {
            try
            {
                using var client = _isapiClientFactoryService.Crear(camara);

                var response = await client.ExecuteAsync(new RestRequest(
                    "/ISAPI/System/Video/inputs/channels/1/VCAResource",
                    Method.Get
                ), ct);

                if (!response.IsSuccessful || string.IsNullOrWhiteSpace(response.Content))
                    return Resultado<VCAModoEnum>.Fallo($"Error al consultar el modo VCA de la cámara {camara.IP}, con código HTTP {response.StatusCode}");


                var doc = XDocument.Parse(response.Content);

                string? typeValue = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "type")?.Value;

                if (string.IsNullOrWhiteSpace(typeValue))
                    return Resultado<VCAModoEnum>.Fallo("La respuesta XML de la cámara no contiene el campo <type>.");

                if (!ModoPorType.TryGetValue(typeValue, out var modo))
                    return Resultado<VCAModoEnum>.Fallo("La cámara tiene un modo VCA activo que no está soportado por esta aplicación.");

                return Resultado<VCAModoEnum>.Ok(modo);
            }
            catch
            {
                return Resultado<VCAModoEnum>.Fallo($"Excepción al obtener el modo VCA actual ({camara.IP})");
            }
        }
    }
}
