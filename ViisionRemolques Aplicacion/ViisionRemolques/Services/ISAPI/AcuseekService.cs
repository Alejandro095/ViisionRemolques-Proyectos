using Microsoft.Extensions.Options;
using RestSharp;
using System.Text.Json;
using System.Xml.Linq;
using ViisionRemolques.Settings;
using ViisionRemolques.Utils;

namespace ViisionRemolques.Services.ISAPI
{
    /// <summary>
    /// Cliente ISAPI para el módulo AcuSeek (búsqueda semántica de video) del MVR.
    /// Nota de firmware: los endpoints bajo /ISAPI/ContentMgmt/* solo aceptan XML
    /// (?format=json rompe con "Invalid XML Content/Format"); los de /ISAPI/Intelligent/*
    /// y /ISAPI/System/* sí aceptan JSON con ?format=json. Verificado contra el MVR real.
    /// </summary>
    public class AcuseekService
    {
        private readonly IsapiClientFactoryService _isapiClientFactoryService;
        private readonly MvrSettings _mvrSettings;

        public AcuseekService(IsapiClientFactoryService isapiClientFactoryService, IOptions<MvrSettings> mvrSettings)
        {
            _isapiClientFactoryService = isapiClientFactoryService;
            _mvrSettings = mvrSettings.Value;
        }

        public async Task<Resultado<(DateTime Hora, string Offset)>> ObtenerHoraDispositivoAsync(CancellationToken ct = default)
        {
            try
            {
                using var client = Crear();
                var request = new RestRequest("/ISAPI/System/time", Method.Get);
                var response = await client.ExecuteAsync(request, ct);

                if (!response.IsSuccessful || string.IsNullOrWhiteSpace(response.Content))
                    return Resultado<(DateTime, string)>.Fallo($"No se pudo leer la hora del MVR ({response.StatusCode}).");

                var doc = XDocument.Parse(response.Content);
                var localTime = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "localTime")?.Value;

                if (string.IsNullOrWhiteSpace(localTime) || localTime.Length < 25)
                    return Resultado<(DateTime, string)>.Fallo("El MVR no devolvió una hora local válida.");

                // e.g. 2026-10-14T20:37:02-06:00
                var offset = localTime[19..];

                if (!DateTime.TryParse(localTime, System.Globalization.CultureInfo.InvariantCulture,
                        System.Globalization.DateTimeStyles.RoundtripKind, out var hora))
                    return Resultado<(DateTime, string)>.Fallo("No se pudo interpretar la hora local del MVR.");

                return Resultado<(DateTime, string)>.Ok((hora, offset));
            }
            catch (Exception ex)
            {
                return Resultado<(DateTime, string)>.Fallo(ex);
            }
        }

        public async Task<Resultado<List<(int Id, string Nombre)>>> ObtenerCanalesAsync(CancellationToken ct = default)
        {
            try
            {
                using var client = Crear();
                // Este endpoint rechaza ?format=json en este firmware; responde XML siempre.
                var request = new RestRequest("/ISAPI/ContentMgmt/InputProxy/channels", Method.Get);
                var response = await client.ExecuteAsync(request, ct);

                if (!response.IsSuccessful || string.IsNullOrWhiteSpace(response.Content))
                    return Resultado<List<(int, string)>>.Fallo($"No se pudo obtener la lista de canales del MVR ({response.StatusCode}).");

                var doc = XDocument.Parse(response.Content);

                var canales = doc.Descendants().Where(e => e.Name.LocalName == "InputProxyChannel")
                    .Select(c => new
                    {
                        Id = c.Elements().FirstOrDefault(x => x.Name.LocalName == "id")?.Value,
                        Nombre = c.Elements().FirstOrDefault(x => x.Name.LocalName == "name")?.Value
                    })
                    .Where(c => int.TryParse(c.Id, out _))
                    .Select(c => (int.Parse(c.Id!), c.Nombre ?? $"Canal {c.Id}"))
                    .ToList();

                return Resultado<List<(int, string)>>.Ok(canales);
            }
            catch (Exception ex)
            {
                return Resultado<List<(int, string)>>.Fallo(ex);
            }
        }

        public async Task<Resultado<string>> IniciarBusquedaTextoAsync(
            string texto, string startTime, string endTime, int[] canales, int similarity, string? idioma, CancellationToken ct = default)
        {
            try
            {
                using var client = Crear();

                var payload = new
                {
                    startTime,
                    endTime,
                    channelList = canales,
                    text = texto.Trim(),
                    language = string.IsNullOrWhiteSpace(idioma) ? "English" : idioma,
                    similarity = Math.Clamp(similarity <= 0 ? 60 : similarity, 0, 100)
                };

                var request = new RestRequest("/ISAPI/ContentMgmt/AsynSearchByTextTask?format=json", Method.Post);
                request.AddJsonBody(payload);

                var response = await client.ExecuteAsync(request, ct);

                if (!response.IsSuccessful || string.IsNullOrWhiteSpace(response.Content))
                    return Resultado<string>.Fallo($"No se pudo iniciar la búsqueda en el MVR ({response.StatusCode}).");

                using var doc = JsonDocument.Parse(response.Content);
                if (!doc.RootElement.TryGetProperty("taskID", out var taskId))
                    return Resultado<string>.Fallo($"El MVR no devolvió taskID: {response.Content}");

                return Resultado<string>.Ok(taskId.GetString() ?? string.Empty);
            }
            catch (Exception ex)
            {
                return Resultado<string>.Fallo(ex);
            }
        }

        /// <summary>Devuelve el JSON crudo del MVR (matchResults incluye filePath de imágenes sin proxear).</summary>
        public async Task<Resultado<string>> ObtenerEstadoBusquedaAsync(
            string taskId, int searchResultPosition, int maxResults, int similarity, CancellationToken ct = default)
        {
            try
            {
                using var client = Crear();

                var payload = new
                {
                    taskID = taskId,
                    searchResultPosition = searchResultPosition <= 0 ? 1 : searchResultPosition,
                    maxResults = maxResults <= 0 ? 18 : maxResults,
                    similarity
                };

                var request = new RestRequest("/ISAPI/ContentMgmt/SearchTextTaskStatus?format=json", Method.Post);
                request.AddJsonBody(payload);

                var response = await client.ExecuteAsync(request, ct);

                if (!response.IsSuccessful || string.IsNullOrWhiteSpace(response.Content))
                    return Resultado<string>.Fallo($"No se pudo consultar el estado de la búsqueda ({response.StatusCode}).");

                return Resultado<string>.Ok(response.Content);
            }
            catch (Exception ex)
            {
                return Resultado<string>.Fallo(ex);
            }
        }

        public async Task<Resultado> EliminarBusquedaAsync(string taskId, CancellationToken ct = default)
        {
            try
            {
                using var client = Crear();

                var request = new RestRequest("/ISAPI/ContentMgmt/DeleteAsynSearchByTextTask?format=json", Method.Post);
                request.AddJsonBody(new { taskID = taskId });

                var response = await client.ExecuteAsync(request, ct);

                if (!response.IsSuccessful || string.IsNullOrWhiteSpace(response.Content))
                    return Resultado.Fallo($"No se pudo eliminar la tarea de búsqueda ({response.StatusCode}).");

                return ValidarSobreEstandar(response.Content);
            }
            catch (Exception ex)
            {
                return Resultado.Fallo(ex);
            }
        }

        public async Task<Resultado<List<string>>> ObtenerHistorialAsync(CancellationToken ct = default)
        {
            try
            {
                using var client = Crear();
                var request = new RestRequest("/ISAPI/Intelligent/AcuSeek/GetHistoricalTerms?format=json", Method.Get);
                var response = await client.ExecuteAsync(request, ct);

                if (!response.IsSuccessful || string.IsNullOrWhiteSpace(response.Content))
                    return Resultado<List<string>>.Fallo($"No se pudo obtener el historial de términos ({response.StatusCode}).");

                using var doc = JsonDocument.Parse(response.Content);
                var lista = new List<string>();

                if (doc.RootElement.TryGetProperty("historicalTermsList", out var arr) && arr.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in arr.EnumerateArray())
                    {
                        if (item.TryGetProperty("terms", out var t) && t.ValueKind == JsonValueKind.String)
                            lista.Add(t.GetString()!);
                    }
                }

                return Resultado<List<string>>.Ok(lista);
            }
            catch (Exception ex)
            {
                return Resultado<List<string>>.Fallo(ex);
            }
        }

        public async Task<Resultado<List<string>>> ObtenerFavoritosAsync(CancellationToken ct = default)
        {
            try
            {
                using var client = Crear();
                var request = new RestRequest("/ISAPI/Intelligent/AcuSeek/GetCollectTerms?format=json", Method.Post);
                request.AddJsonBody(new { });

                var response = await client.ExecuteAsync(request, ct);

                if (!response.IsSuccessful || string.IsNullOrWhiteSpace(response.Content))
                    return Resultado<List<string>>.Fallo($"No se pudo obtener la lista de favoritos ({response.StatusCode}).");

                using var doc = JsonDocument.Parse(response.Content);
                var lista = new List<string>();

                if (doc.RootElement.TryGetProperty("termsList", out var arr) && arr.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in arr.EnumerateArray())
                    {
                        var texto = item.TryGetProperty("text", out var t) && t.ValueKind == JsonValueKind.String
                            ? t.GetString()
                            : item.TryGetProperty("terms", out var t2) && t2.ValueKind == JsonValueKind.String ? t2.GetString() : null;

                        if (!string.IsNullOrWhiteSpace(texto))
                            lista.Add(texto);
                    }
                }

                return Resultado<List<string>>.Ok(lista);
            }
            catch (Exception ex)
            {
                return Resultado<List<string>>.Fallo(ex);
            }
        }

        /// <summary>collectStatus: 1 = agregar, 2 = quitar.</summary>
        public async Task<Resultado> EstablecerFavoritoAsync(string terminos, bool agregar, CancellationToken ct = default)
        {
            try
            {
                using var client = Crear();
                var request = new RestRequest("/ISAPI/Intelligent/AcuSeek/CollectTerms?format=json", Method.Post);
                request.AddJsonBody(new { terms = terminos, collectStatus = agregar ? 1 : 2 });

                var response = await client.ExecuteAsync(request, ct);

                if (!response.IsSuccessful || string.IsNullOrWhiteSpace(response.Content))
                    return Resultado.Fallo($"No se pudo actualizar el favorito ({response.StatusCode}).");

                return ValidarSobreEstandar(response.Content);
            }
            catch (Exception ex)
            {
                return Resultado.Fallo(ex);
            }
        }

        public async Task<Resultado<string>> EnviarTuningAsync(
            string texto, string[] positivas, string[] negativas, string? idioma, CancellationToken ct = default)
        {
            try
            {
                using var client = Crear();

                var payload = new
                {
                    type = "text",
                    text = texto,
                    language = string.IsNullOrWhiteSpace(idioma) ? "English" : idioma,
                    positiveImageList = positivas,
                    negativeImageList = negativas
                };

                var request = new RestRequest("/ISAPI/Intelligent/ModelFineTuning/AddModelFineTuning?format=json", Method.Post);
                request.AddJsonBody(payload);

                var response = await client.ExecuteAsync(request, ct);

                if (!response.IsSuccessful || string.IsNullOrWhiteSpace(response.Content))
                    return Resultado<string>.Fallo($"No se pudo enviar el ajuste fino (tuning) ({response.StatusCode}).");

                using var doc = JsonDocument.Parse(response.Content);
                var resultId = doc.RootElement.TryGetProperty("resultID", out var r) ? r.GetString() : null;

                return Resultado<string>.Ok(resultId ?? string.Empty);
            }
            catch (Exception ex)
            {
                return Resultado<string>.Fallo(ex);
            }
        }

        /// <summary>Devuelve el JSON crudo con matchResults[0].{percent, fineTuningStatus}.</summary>
        public async Task<Resultado<string>> ObtenerEstadoTuningAsync(string texto, CancellationToken ct = default)
        {
            try
            {
                using var client = Crear();

                var payload = new
                {
                    searchID = Guid.NewGuid().ToString().ToUpperInvariant(),
                    text = texto,
                    searchResultPosition = 1,
                    maxResults = 1
                };

                var request = new RestRequest("/ISAPI/Intelligent/ModelFineTuning/SearchModelFineTuning?format=json", Method.Post);
                request.AddJsonBody(payload);

                var response = await client.ExecuteAsync(request, ct);

                if (!response.IsSuccessful || string.IsNullOrWhiteSpace(response.Content))
                    return Resultado<string>.Fallo($"No se pudo consultar el estado del tuning ({response.StatusCode}).");

                return Resultado<string>.Ok(response.Content);
            }
            catch (Exception ex)
            {
                return Resultado<string>.Fallo(ex);
            }
        }

        /// <summary>Descarga una imagen/snapshot (filePath absoluto del MVR) autenticada por Digest.</summary>
        public async Task<Resultado<(byte[] Bytes, string ContentType)>> ObtenerImagenAsync(string url, CancellationToken ct = default)
        {
            try
            {
                if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Host != _mvrSettings.IP)
                    return Resultado<(byte[], string)>.Fallo("La URL de la imagen no pertenece al MVR configurado.");

                using var client = Crear();
                var request = new RestRequest(uri, Method.Get);
                var response = await client.ExecuteAsync(request, ct);

                if (!response.IsSuccessful || response.RawBytes is null || response.RawBytes.Length == 0)
                    return Resultado<(byte[], string)>.Fallo($"No se pudo descargar la imagen del MVR ({response.StatusCode}).");

                var contentType = response.ContentType ?? "image/jpeg";
                return Resultado<(byte[], string)>.Ok((response.RawBytes, contentType));
            }
            catch (Exception ex)
            {
                return Resultado<(byte[], string)>.Fallo(ex);
            }
        }

        private RestClient Crear() =>
            _isapiClientFactoryService.Crear(_mvrSettings.IP, _mvrSettings.Usuario, _mvrSettings.Secreto);

        /// <summary>El sobre estándar de ISAPI usa errorCode:1/errorMsg:"ok" para ÉXITO (no es un error).</summary>
        private static Resultado ValidarSobreEstandar(string json)
        {
            using var doc = JsonDocument.Parse(json);
            var errorCode = doc.RootElement.TryGetProperty("errorCode", out var e) ? e.GetInt64() : 0;
            var errorMsg = doc.RootElement.TryGetProperty("errorMsg", out var m) ? m.GetString() : null;

            return errorCode == 1 ? Resultado.Ok() : Resultado.Fallo(errorMsg ?? $"El MVR devolvió un error (errorCode {errorCode}).");
        }
    }
}
