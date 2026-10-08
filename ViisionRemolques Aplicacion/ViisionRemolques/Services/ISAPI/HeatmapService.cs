using RestSharp;
using System.Xml.Linq;
using ViisionRemolques.Parsing;
using ViisionRemolques.Repositories;
using ViisionRemolques.Utils;

namespace ViisionRemolques.Services.ISAPI
{
    public class HeatmapService
    {
        private readonly ISAPIClientFactoryService _isapiClientFactoryService;
        private readonly AlmacenamientoImagenesService _almacenamientoImagenesService;

        public HeatmapService(
            ISAPIClientFactoryService isapiClientFactoryService,
            AlmacenamientoImagenesService almacenamientoImagenesService)
        {
            _isapiClientFactoryService = isapiClientFactoryService;
            _almacenamientoImagenesService = almacenamientoImagenesService;
        }

        public readonly string[] tiposReportesValidos = ["daily", "weekly", "monthly", "yearly"];
        public readonly string[] tiposModelosEstadisticosValidos = ["duration", "PDC"];

        public async Task<Resultado<HeatmapInformacion>> ObtenerHeatmap(
            CamaraEntity camara,
            HeatmapInformacionRequest request,
            CancellationToken ct = default)
        {
            try
            {
                if (request.TipoReporte is null || !tiposReportesValidos.Contains(request.TipoReporte))
                {
                    return Resultado<HeatmapInformacion>.Fallo($"Tipo de reporte inválido ({string.Join(", ", tiposReportesValidos)})");
                }

                if (request.ModeloEstadistico is null || !tiposModelosEstadisticosValidos.Contains(request.ModeloEstadistico))
                    return Resultado<HeatmapInformacion>.Fallo($"Modelo estadístico inválido ({string.Join(", ", tiposModelosEstadisticosValidos)})");

                var heatmapHabilitadoResultado = await ValidarHeatmapActivoAsync(camara, ct);

                if (!heatmapHabilitadoResultado.Exito)
                    return Resultado<HeatmapInformacion>.Fallo(heatmapHabilitadoResultado.Error ?? $"No fue posible consultar el estado del servicio Heatmap para la cámara ({camara?.IP ?? "IP no especificada"}).");

                if (!heatmapHabilitadoResultado.Valor)
                    return Resultado<HeatmapInformacion>.Fallo($"La funcionalidad Heatmap se encuentra deshabilitada en la cámara ({camara?.IP ?? "IP no especificada"}).");

                var fechaObjetivo = request.Fecha ?? DateTime.Now;
                var (fechaInicio, fechaFinal) = FechasCanonicasUtils.Obtener(request.TipoReporte, fechaObjetivo);

                var dataRequest = new HeatmapDataInformacionRequest
                {
                    TipoReporte = request.TipoReporte,
                    ModeloEstadistico = request.ModeloEstadistico,
                    FechaInicio = fechaInicio,
                    FechaFinal = fechaFinal,
                };

                var minMaxResultado = await ObtenerMinMaxHeatmapAsync(camara, dataRequest, ct);

                if (!minMaxResultado.Exito)
                    return Resultado<HeatmapInformacion>.Fallo(minMaxResultado.Error ?? $"Error al consultar los parámetros Min/Max del heatmap ({camara?.IP ?? "IP no especificada"})");

                var imagenResultado = await ObtenerImagenHeatmapAsync(camara, dataRequest, ct);

                if (!imagenResultado.Exito)
                    return Resultado<HeatmapInformacion>.Fallo(imagenResultado.Error ?? $"Error al descargar el heatmap ({camara?.IP ?? "IP no especificada"})");

                var imagenPath = await _almacenamientoImagenesService.Guardar(new List<byte[]>
                {
                    imagenResultado.Valor!
                }, cancellationToken: ct);

                if (imagenPath is null || imagenPath.Count == 0)
                    return Resultado<HeatmapInformacion>.Fallo($"Error al guardar la imagen del heatmap ({camara?.IP ?? "IP no especificada"})");

                return Resultado<HeatmapInformacion>.Ok(new HeatmapInformacion
                {
                    Fecha = fechaObjetivo,
                    TipoReporte = request.TipoReporte,
                    ModeloEstadistico = request.ModeloEstadistico,
                    Habilitado = true,
                    Min = minMaxResultado.Valor!.Min,
                    Max = minMaxResultado.Valor!.Max,
                    ImagenPath = imagenPath[0]
                });

            }
            catch (Exception ex)
            {
                return Resultado<HeatmapInformacion>.Fallo($"Excepción no controlada: {ex.Message}");
            }
        }

        public async Task<Resultado<bool>> ValidarHeatmapActivoAsync(CamaraEntity camara, CancellationToken ct = default)
        {
            try
            {
                using var client = _isapiClientFactoryService.Crear(camara);
                var request = new RestRequest("/ISAPI/System/Video/inputs/channels/1/heatMap", Method.Get);
                var response = await client.ExecuteAsync(request, ct);

                if (!response.IsSuccessful || string.IsNullOrWhiteSpace(response.Content))
                    return Resultado<bool>.Fallo($"Respuesta HTTP inválida o vacía al consultar estado de heatmap ({response.StatusCode}).");

                XDocument doc = XDocument.Parse(response.Content);
                string? value = doc.Buscar("//*[local-name()='enabled']");

                if (bool.TryParse(value, out bool result))
                    return Resultado<bool>.Ok(result);

                return Resultado<bool>.Fallo("No se pudo analizar el nodo 'enabled' de la respuesta XML.");
            }
            catch (Exception ex)
            {
                return Resultado<bool>.Fallo($"Error de red o parseo al validar estado: {ex.Message}");
            }
        }

        public async Task<Resultado<HeatmapMinMaxResponse>> ObtenerMinMaxHeatmapAsync(
            CamaraEntity camara,
            HeatmapDataInformacionRequest heatmapDataInformacionRequest,
            CancellationToken ct = default)
        {
            try
            {
                using var client = _isapiClientFactoryService.Crear(camara);

                var request = new RestRequest($"/ISAPI/System/Video/inputs/channels/1/heatMap/pictureInfo", Method.Post);

                var xmlPayload = new XDocument(
                    new XDeclaration("1.0", "utf-8", null),
                    new XElement("HeatMapDataDescription",
                        new XElement("reportType", heatmapDataInformacionRequest.TipoReporte),
                        new XElement("timeSpanList",
                            new XElement("timeSpan",
                                new XElement("startTime", heatmapDataInformacionRequest.FechaInicio.ToString("yyyy-MM-ddTHH:mm:ss")),
                                new XElement("endTime", heatmapDataInformacionRequest.FechaFinal.ToString("yyyy-MM-ddTHH:mm:ss"))
                            )
                        ),
                        new XElement("statisticalModel", heatmapDataInformacionRequest.ModeloEstadistico)
                    )
                );

                request.AddHeader("Content-Type", "application/xml");
                request.AddStringBody(xmlPayload.ToString(), DataFormat.Xml);

                var response = await client.ExecuteAsync(request, ct);

                if (!response.IsSuccessful || string.IsNullOrWhiteSpace(response.Content))
                    return Resultado<HeatmapMinMaxResponse>.Fallo($"Respuesta HTTP inválida o vacía al consultar valores Min/Max ({response.StatusCode}).");

                XDocument doc = XDocument.Parse(response.Content);

                string? maxStr = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "MaxValue")?.Value;
                string? minStr = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "MinValue")?.Value;

                if (double.TryParse(maxStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double max) &&
                    double.TryParse(minStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double min))
                {
                    return Resultado<HeatmapMinMaxResponse>.Ok(new HeatmapMinMaxResponse
                    {
                        Min = min,
                        Max = max
                    });
                }

                return Resultado<HeatmapMinMaxResponse>.Fallo("Los nodos MinValue o MaxValue no se encontraron o tenían un formato numérico incorrecto.");
            }
            catch (Exception ex)
            {
                return Resultado<HeatmapMinMaxResponse>.Fallo($"Error de red o parseo al obtener Min/Max: {ex.Message}");
            }
        }

        public async Task<Resultado<byte[]>> ObtenerImagenHeatmapAsync(
            CamaraEntity camara,
            HeatmapDataInformacionRequest heatMapDataInformacionRequest,
            CancellationToken ct = default)
        {
            try
            {
                using var client = _isapiClientFactoryService.Crear(camara);

                var fechaInicio = heatMapDataInformacionRequest.FechaInicio.ToString("s");
                var fechaFinal = heatMapDataInformacionRequest.FechaFinal.ToString("s");

                var request = new RestRequest($"/ISAPI/System/Video/inputs/channels/1/heatMap/picture?starttime={fechaInicio}&endtime={fechaFinal}&statisticalModel={heatMapDataInformacionRequest.ModeloEstadistico}", Method.Get);

                var response = await client.ExecuteAsync(request, ct);

                if (!response.IsSuccessful)
                    return Resultado<byte[]>.Fallo($"Error HTTP al descargar la imagen ({response.StatusCode}).");

                if (response.ContentType != null && response.ContentType.Contains("xml", StringComparison.OrdinalIgnoreCase))
                    return Resultado<byte[]>.Fallo("La cámara retornó un documento XML (posible error) en lugar de una imagen binaria.");

                if (response.RawBytes == null || response.RawBytes.Length == 0)
                    return Resultado<byte[]>.Fallo("La cámara no retornó datos binarios para la imagen.");

                return Resultado<byte[]>.Ok(response.RawBytes);
            }
            catch (Exception ex)
            {
                return Resultado<byte[]>.Fallo($"Excepción al obtener la imagen del heatmap: {ex.Message}");
            }
        }

        public class HeatmapInformacion
        {
            public DateTime Fecha { get; set; }
            public bool Habilitado { get; set; } = false;
            public string? ImagenPath { get; set; }
            public double Max { get; set; }
            public double Min { get; set; }
            public string? ModeloEstadistico { get; set; }
            public string? TipoReporte { get; set; }
        }

        public class HeatmapInformacionRequest
        {
            public DateTime? Fecha { get; set; }
            public string? ModeloEstadistico { get; set; }
            public string? TipoReporte { get; set; }
        }

        public class HeatmapDataInformacionRequest
        {
            public string TipoReporte { get; set; } = "daily";
            public string ModeloEstadistico { get; set; } = "duration"; //PDC,duration
            public DateTime FechaInicio { get; set; } = DateTime.Now;
            public DateTime FechaFinal { get; set; } = DateTime.Now;
        }

        public class HeatmapMinMaxResponse
        {
            public double Min { get; set; }
            public double Max { get; set; }
        }
    }
}