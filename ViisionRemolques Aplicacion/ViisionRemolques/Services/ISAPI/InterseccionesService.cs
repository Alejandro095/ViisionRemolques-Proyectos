using RestSharp;
using System.Text.Json;
using System.Text.Json.Serialization;
using ViisionRemolques.Repositories;
using ViisionRemolques.Utils;

namespace ViisionRemolques.Services.ISAPI
{
    public class InterseccionesService
    {
        private readonly ISAPIClientFactoryService _isapiClientFactoryService;
        private readonly AlmacenamientoImagenesService _almacenamientoImagenesService;

        public InterseccionesService(
            ISAPIClientFactoryService ISAPIClientFactoryService,
            AlmacenamientoImagenesService almacenamientoImagenesService)
        {
            _isapiClientFactoryService = ISAPIClientFactoryService;
            _almacenamientoImagenesService = almacenamientoImagenesService;
        }

        public readonly string[] tiposReportesValidos = ["daily", "weekly", "monthly", "yearly"];

        public async Task<Resultado<InterseccionInformacion>> ObtenerInterseccionAsync(
            CamaraEntity camara,
            InterseccionInformacionRequest request,
            CancellationToken ct = default)
        {
            try
            {
                if (request.TipoReporte is null || !tiposReportesValidos.Contains(request.TipoReporte))
                {
                    return Resultado<InterseccionInformacion>.Fallo($"Tipo de reporte inválido ({string.Join(", ", tiposReportesValidos)})");
                }

                var configuracionResultado = await ObtenerConfiguracionAsync(camara, ct);

                if (!configuracionResultado.Exito)
                {
                    return Resultado<InterseccionInformacion>.Fallo(configuracionResultado.Error ?? $"No fue posible consultar el estado del servicio Intersecciones para la cámara ({camara?.IP ?? "IP no especificada"}).");
                }

                var interseccionConfiguracion = configuracionResultado.Valor!;

                if (interseccionConfiguracion.Accesos.Count == 0)
                {
                    return Resultado<InterseccionInformacion>.Fallo($"La cámara ({camara?.IP ?? "IP no especificada"}) no tiene accesos configurados para intersecciones.");
                }

                if (interseccionConfiguracion.Habilitado is false)
                {
                    return Resultado<InterseccionInformacion>.Fallo($"La funcionalidad Intersecciones se encuentra deshabilitada en la cámara ({camara?.IP ?? "IP no especificada"}).");
                }

                if (request.Entrada is not null && !interseccionConfiguracion.Accesos.Contains(request.Entrada))
                {
                    return Resultado<InterseccionInformacion>.Fallo($"El valor de la entrada es inválido, solo es posible: {string.Join(", ", interseccionConfiguracion.Accesos)}.");
                }

                var fechaObjetivo = request.Fecha ?? DateTime.Now;
                var (fechaInicio, fechaFinal) = FechasCanonicasUtils.Obtener(request.TipoReporte, fechaObjetivo);

                var flujosRequest = new FlujosInterseccionRequest
                {
                    Entrada = request.Entrada,
                    TipoReporte = request.TipoReporte,
                    FechaInicio = fechaInicio,
                    FechaFinal = fechaFinal,
                    Accesos = interseccionConfiguracion.Accesos
                };

                var flujosResultado = await ObtenerFlujosInterseccionAsync(camara, flujosRequest, ct);

                if (!flujosResultado.Exito)
                {
                    return Resultado<InterseccionInformacion>.Fallo(flujosResultado.Error ?? $"No se han podido obtener los flujos en la cámara {camara?.IP ?? "IP no especificada"}");
                }

                return Resultado<InterseccionInformacion>.Ok(new InterseccionInformacion
                {
                    Fecha = fechaObjetivo,
                    TipoReporte = request.TipoReporte,
                    Entrada = request.Entrada,
                    Habilitado = true,
                    Intersecciones = flujosResultado.Valor!
                });
            }
            catch (Exception ex)
            {
                return Resultado<InterseccionInformacion>.Fallo($"Excepción no controlada: {ex.Message}");
            }
        }

        public async Task<Resultado<List<FlujoInterseccion>>> ObtenerFlujosInterseccionAsync(
            CamaraEntity camara,
            FlujosInterseccionRequest flujosInterseccionRequest,
            CancellationToken ct = default)
        {
            try
            {
                using var client = _isapiClientFactoryService.Crear(camara);

                var request = new RestRequest("/ISAPI/Intelligent/channels/1/intersectionAnalysis/search?format=json", Method.Post);
                request.AddHeader("Content-Type", "application/json");
                request.AddStringBody(JsonSerializer.Serialize(new
                {
                    searchID = Guid.NewGuid().ToString().ToUpperInvariant(),
                    searchResultPosition = 0,
                    maxResultNumber = 10000,
                    maxResults = 10000,
                    reportType = flujosInterseccionRequest.TipoReporte,
                    startTime = flujosInterseccionRequest.FechaInicio.ToString("s"),
                    endTime = flujosInterseccionRequest.FechaFinal.ToString("s"),
                    entranceID = flujosInterseccionRequest.Entrada ?? ""
                }), DataFormat.Json);

                var response = await client.ExecuteAsync(request, ct);

                if (!response.IsSuccessful || string.IsNullOrWhiteSpace(response.Content))
                {
                    return Resultado<List<FlujoInterseccion>>.Fallo($"Error HTTP al buscar flujos de intersección ({response.StatusCode}).");
                }

                var data = JsonSerializer.Deserialize<InterseccionSearchResponse>(response.Content);

                if (data?.Data is null)
                {
                    return Resultado<List<FlujoInterseccion>>.Ok(new List<FlujoInterseccion>());
                }

                var resultados = data.Data
                    .Where(f => f.EndID is not null && flujosInterseccionRequest.Accesos.Contains(f.EndID))
                    .Select(f => new FlujoInterseccion
                    {
                        Origen = f.StartID,
                        Destino = f.EndID,
                        Personas = f.PDC
                    })
                    .ToList();

                return Resultado<List<FlujoInterseccion>>.Ok(resultados);
            }
            catch (Exception ex)
            {
                return Resultado<List<FlujoInterseccion>>.Fallo($"Error al obtener flujos de intersección: {ex.Message}");
            }
        }

        public async Task<Resultado<InterseccionConfiguracion>> ObtenerConfiguracionAsync(
            CamaraEntity camara,
            CancellationToken ct = default)
        {
            try
            {
                using var client = _isapiClientFactoryService.Crear(camara);

                var request = new RestRequest("/ISAPI/Intelligent/channels/1/intersectionAnalysis?format=json", Method.Get);

                var response = await client.ExecuteAsync(request, ct);

                if (!response.IsSuccessful || string.IsNullOrWhiteSpace(response.Content))
                {
                    return Resultado<InterseccionConfiguracion>.Fallo($"Error HTTP al obtener configuración de intersección ({response.StatusCode}).");
                }

                var data = JsonSerializer.Deserialize<IntersectionResponse>(response.Content);
                var analysis = data?.IntersectionAnalysis;

                if (analysis is null)
                {
                    return Resultado<InterseccionConfiguracion>.Fallo("El JSON de configuración de intersección no tiene el formato esperado o está vacío.");
                }

                var config = new InterseccionConfiguracion
                {
                    Habilitado = analysis.Enabled,
                    Accesos = analysis.TagID?
                        .Select(t => t.ID)
                        .OfType<string>()
                        .Select(id => id.ToUpperInvariant())
                        .ToList() ?? new List<string>()
                };

                return Resultado<InterseccionConfiguracion>.Ok(config);
            }
            catch (Exception ex)
            {
                return Resultado<InterseccionConfiguracion>.Fallo($"Error al parsear o solicitar la configuración de intersección: {ex.Message}");
            }
        }

        public class FlujosInterseccionRequest
        {
            public DateTime FechaInicio { get; set; }
            public DateTime FechaFinal { get; set; }
            public string? TipoReporte { get; set; }
            public string? Entrada { get; set; }
            public List<string> Accesos { get; set; } = new List<string>();
        }

        public class InterseccionInformacionRequest
        {
            public DateTime? Fecha { get; set; }
            public string? TipoReporte { get; set; }
            public string? Entrada { get; set; }
        }

        public class FlujoInterseccion
        {
            public string? Origen { get; set; }
            public string? Destino { get; set; }
            public int Personas { get; set; }
        }

        public class InterseccionInformacion
        {
            public DateTime Fecha { get; set; }
            public bool Habilitado { get; set; } = false;
            public string? TipoReporte { get; set; }
            public string? Entrada { get; set; }
            public List<FlujoInterseccion> Intersecciones { get; set; } = new List<FlujoInterseccion>();
        }

        public class InterseccionConfiguracion
        {
            public bool Habilitado { get; set; }
            public List<string> Accesos { get; set; } = new List<string>();
        }

        public class InterseccionSearchResponse
        {
            [JsonPropertyName("responseStatusStrg")]
            public string? ResponseStatusStrg { get; set; }

            [JsonPropertyName("Data")]
            public List<FlujoItem>? Data { get; set; }
        }

        public class FlujoItem
        {
            [JsonPropertyName("startID")]
            public string? StartID { get; set; }

            [JsonPropertyName("endID")]
            public string? EndID { get; set; }

            [JsonPropertyName("PDC")]
            public int PDC { get; set; }
        }

        public class IntersectionResponse
        {
            [JsonPropertyName("IntersectionAnalysis")]
            public IntersectionAnalysisData? IntersectionAnalysis { get; set; }
        }

        public class IntersectionAnalysisData
        {
            [JsonPropertyName("enabled")]
            public bool Enabled { get; set; }

            [JsonPropertyName("TagID")]
            public List<TagItem>? TagID { get; set; }
        }

        public class TagItem
        {
            [JsonPropertyName("ID")]
            public string? ID { get; set; }
        }
    }
}