using RestSharp;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml.Linq;
using System.Xml.Serialization;
using ViisionRemolques.Entities;
using ViisionRemolques.Parsing;
using ViisionRemolques.Utils;
using static ViisionRemolques.Services.ISAPI.HeatmapService;

namespace ViisionRemolques.Services.ISAPI
{
    public class InterseccionesService
    {
        private readonly ISAPIClientFactoryService _clientFactory;
        private readonly AlmacenamientoImagenesService _almacenamientoImagenesService;
        public InterseccionesService(
            ISAPIClientFactoryService ISAPIClientFactoryService, 
            AlmacenamientoImagenesService almacenamientoImagenesService) {
            _clientFactory = ISAPIClientFactoryService;
            _almacenamientoImagenesService = almacenamientoImagenesService;
        }

        public readonly string[] tiposReportesValidos = ["daily", "weekly", "monthly", "yearly"];

        public async Task<object?> ObtenerInterseccionAsync(
            Camara camara,
            InterseccionInformacionRequest interseccionInformacionRequest, 
            CancellationToken ct = default)
        {
            var interseccion = new InterseccionInformacion
            {
                Fecha = interseccionInformacionRequest.Fecha ?? DateTime.Now,
                TipoReporte = interseccionInformacionRequest.TipoReporte,
                Entrada = interseccionInformacionRequest.Entrada,
            };

            try
            {
                if (interseccion.TipoReporte is null || !tiposReportesValidos.Contains(interseccion.TipoReporte))
                {
                    interseccion.Error = true;
                    interseccion.ErrorMensaje = $"Tipo de reporte inválido ({string.Join(", ", tiposReportesValidos)})";

                    return interseccion;
                }

                var interseccionConfiguracion = await ObtenerConfiguracionAsync(camara, ct);

                if (interseccionConfiguracion is null || interseccionConfiguracion.Accesos.Count == 0)
                {
                    interseccion.Error = true;
                    interseccion.ErrorMensaje = $"No fue posible consultar el estado del servicio Intersecciones para la cámara ({camara?.IP ?? "IP no especificada"}).";

                    return interseccion;
                }

                if (interseccionConfiguracion.Habilitado is false)
                {
                    interseccion.Error = true;
                    interseccion.ErrorMensaje = $"La funcionalidad Heatmap se encuentra deshabilitada en la cámara ({camara?.IP ?? "IP no especificada"}).";

                    return interseccion;
                }
                else
                {
                    interseccion.Habilitado = true;
                }

                if (interseccion.Entrada is not null && !interseccionConfiguracion.Accesos.Contains(interseccion.Entrada))
                {
                    interseccion.Error = true;
                    interseccion.ErrorMensaje = $"El valor de la entrada es invalido, solo es posible: {string.Join(", ", interseccionConfiguracion.Accesos)}.";

                    return interseccion;
                }

                var (fechaInicio, fechaFinal) = FechasCanonicasUtils.Obtener(interseccion.TipoReporte, interseccion.Fecha);

                var flujosInterseccionResponse = await ObtenerFlujosInterseccionAsync(camara, new FlujosInterseccionRequest
                {
                    Entrada = interseccion.Entrada,
                    TipoReporte = interseccion.TipoReporte,
                    FechaInicio = fechaInicio,
                    FechaFinal = fechaFinal,
                    Accesos = interseccionConfiguracion.Accesos
                }, ct);

                if (flujosInterseccionResponse is null || flujosInterseccionResponse.Count == 0)
                {
                    interseccion.Error = true;
                    interseccion.ErrorMensaje = $"No se han podido obtener los flujos en la camara {camara?.IP ?? "IP no especificada"}";
                }

                interseccion.Intersecciones = flujosInterseccionResponse;
                interseccion.Error = false;

                return interseccion;
            }
            catch (Exception ex)
            {
                interseccion.Error = true;
                interseccion.ErrorMensaje = $"Excepcion: {ex.Message}";

                return interseccion;
            }
        }

        public async Task<List<FlujoInterseccion>?> ObtenerFlujosInterseccionAsync(Camara camara, FlujosInterseccionRequest flujosInterseccionRequest, CancellationToken ct = default)
        {
            try
            {
                using var client = _clientFactory.Crear(camara);

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
                    return null;
                }

                var data = JsonSerializer.Deserialize<InterseccionSearchResponse>(response.Content);

                if (data?.Data is null) return null;

                return data.Data
                    .Where(f => f.EndID is not null && flujosInterseccionRequest.Accesos.Contains(f.EndID))
                    .Select(f => new FlujoInterseccion
                    {
                        Origen = f.StartID,
                        Destino = f.EndID,
                        Personas = f.PDC
                    })
                    .ToList();
            }
            catch
            {
                return null;
            }
        }

        public async Task<InterseccionConfiguracion?> ObtenerConfiguracionAsync(Camara camara, CancellationToken ct = default)
        {
            try
            {
                using var client = _clientFactory.Crear(camara);

                var request = new RestRequest("/ISAPI/Intelligent/channels/1/intersectionAnalysis?format=json", Method.Get);

                var response = await client.ExecuteAsync(request, ct);

                if (!response.IsSuccessful || string.IsNullOrWhiteSpace(response.Content))
                {
                    return null;
                }
                var data = JsonSerializer.Deserialize<IntersectionResponse>(response.Content);

                var analysis = data?.IntersectionAnalysis;

                if (analysis is null) return null;

                return new InterseccionConfiguracion
                {
                    Habilitado = analysis.Enabled,
                    Accesos = analysis.TagID?
                        .Select(t => t.ID)
                        .OfType<string>()
                        .Select(id => id.ToUpperInvariant())
                        .ToList() ?? new()
                };
            }
            catch
            {
                return null;
            }
        }

        public class FlujosInterseccionRequest
        {
            public DateTime FechaInicio { get; set; }
            public DateTime FechaFinal { get; set; }
            public string? TipoReporte { get; set; }
            public string? Entrada { get; set; }
            public List<string> Accesos = new List<string>();
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
            public bool Error { get; set; } = true;
            public string? ErrorMensaje { get; set; }
            public DateTime Fecha { get; set; }
            public bool Habilitado { get; set; } = false;
            public string? TipoReporte { get; set; }
            public string? Entrada { get; set; }
            public List<FlujoInterseccion> Intersecciones  { get; set; }
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
