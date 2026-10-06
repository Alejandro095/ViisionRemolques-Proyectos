using RestSharp;
using System.Xml.Linq;
using System.Xml.Serialization;
using ViisionRemolques.Entities;
using ViisionRemolques.Parsing;
using ViisionRemolques.Utils;

namespace ViisionRemolques.Services.ISAPI
{
    public class HeatmapService
    {
        private readonly ISAPIClientFactoryService _clientFactory;
        private readonly AlmacenamientoImagenesService _almacenamientoImagenesService;
        public HeatmapService(
            ISAPIClientFactoryService ISAPIClientFactoryService, 
            AlmacenamientoImagenesService almacenamientoImagenesService) {
            _clientFactory = ISAPIClientFactoryService;
            _almacenamientoImagenesService = almacenamientoImagenesService;
        }

        public readonly string[] tiposReportesValidos = ["daily", "weekly", "monthly", "yearly"];
        public readonly string[] tiposModelosEstadisticosValidos = ["duration", "PDC"];

        public async Task<HeatmapInformacion> ObtenerHeatmap(
            Camara camara, 
            HeatmapInformacionRequest heatmapInformacionRequest, 
            CancellationToken ct = default)
        {
            var heatmap = new HeatmapInformacion
            {
                Fecha = heatmapInformacionRequest.Fecha ?? DateTime.Now,
                TipoReporte = heatmapInformacionRequest.TipoReporte,
                ModeloEstadistico = heatmapInformacionRequest.ModeloEstadistico,
            };

            try
            {
                if (heatmap.TipoReporte is null || !tiposReportesValidos.Contains(heatmap.TipoReporte))
                {
                    heatmap.Error = true;
                    heatmap.ErrorMensaje = $"Tipo de reporte inválido ({string.Join(", ", tiposReportesValidos)})";

                    return heatmap;
                }

                var heatmapHabilitado = await ValidarHeatmapActivoAsync(camara, ct);

                if (heatmapHabilitado is null)
                {
                    heatmap.Error = true;
                    heatmap.ErrorMensaje = $"No fue posible consultar el estado del servicio Heatmap para la cámara ({camara?.IP ?? "IP no especificada"}).";

                    return heatmap;
                }

                if (heatmapHabilitado is false)
                {
                    heatmap.Error = true;
                    heatmap.ErrorMensaje = $"La funcionalidad Heatmap se encuentra deshabilitada en la cámara ({camara?.IP ?? "IP no especificada"}).";

                    return heatmap;
                } else
                {
                    heatmap.HeatmapHabilitado = true;
                }

                var (fechaInicio, fechaFinal) = FechasCanonicasUtils.Obtener(heatmap.TipoReporte, heatmap.Fecha);

                if (heatmap.ModeloEstadistico is null || !tiposModelosEstadisticosValidos.Contains(heatmap.ModeloEstadistico))
                {
                    heatmap.Error = true;
                    heatmap.ErrorMensaje = $"Modelo estadístico inválido ({string.Join(", ", tiposModelosEstadisticosValidos)})";

                    return heatmap;
                }

                var heatmapDataInformacionRequest = new HeatmapDataInformacionRequest
                {
                    TipoReporte = heatmap.TipoReporte,
                    ModeloEstadistico = heatmap.ModeloEstadistico,
                    FechaInicio = fechaInicio,
                    FechaFinal = fechaFinal,
                };

                var minmax = await ObtenerMinMaxHeatmapAsync(camara, heatmapDataInformacionRequest, ct);

                if (minmax is null)
                {
                    heatmap.Error = true;
                    heatmap.ErrorMensaje = $"Error al consultar los parámetros Min/Max del heatmap ({camara?.IP ?? "IP no especificada"})";

                    return heatmap;
                }

                heatmap.Min = minmax.Min;
                heatmap.Max = minmax.Max;

                var imagenBytes = await ObtenerImagenHeatmapAsync(camara, heatmapDataInformacionRequest, ct);

                if (imagenBytes is null)
                {
                    heatmap.Error = true;
                    heatmap.ErrorMensaje = $"Error al descargar el heatmap ({camara?.IP ?? "IP no especificada"})";

                    return heatmap;
                }

                var imagenPath = await _almacenamientoImagenesService.Guardar(new List<byte[]>
                {
                    imagenBytes
                }, cancellationToken: ct);

                if (imagenPath is null || imagenPath.Count == 0)
                {
                    heatmap.Error = true;
                    heatmap.ErrorMensaje = $"Error al guardar la imagen el heatmap ({camara?.IP ?? "IP no especificada"})";

                    return heatmap;
                }

                heatmap.ImagenPath = imagenPath[0];
                heatmap.Error = false;

                return heatmap;

            } catch(Exception ex)
            {
                heatmap.Error = true;
                heatmap.ErrorMensaje = $"Excepcion: {ex.Message}";

                return heatmap;
            }
        }

        public async Task<bool?> ValidarHeatmapActivoAsync(Camara camara, CancellationToken ct = default)
        {
            try
            {
                using var client = _clientFactory.Crear(camara);

                var request = new RestRequest("/ISAPI/System/Video/inputs/channels/1/heatMap", Method.Get);

                var response = await client.ExecuteAsync(request, ct);

                if (!response.IsSuccessful || string.IsNullOrWhiteSpace(response.Content))
                {
                    return null;
                }

                XDocument doc = XDocument.Parse(response.Content);

                string? value = doc.Buscar("//*[local-name()='enabled']");

                return bool.TryParse(value, out bool result) && result;
            }
            catch
            {
                return null;
            }
        }

        public async Task<HeatmapMinMaxResponse?> ObtenerMinMaxHeatmapAsync(
        Camara camara,
        HeatmapDataInformacionRequest heatmapDataInformacionRequest,
        CancellationToken ct = default)
        {
            try
            {
                using var client = _clientFactory.Crear(camara);

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
                {
                    return null;
                }

                // Parsear respuesta
                XDocument doc = XDocument.Parse(response.Content);

                string? maxStr = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "MaxValue")?.Value;
                string? minStr = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "MinValue")?.Value;

                if (double.TryParse(maxStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double max) &&
                    double.TryParse(minStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double min))
                {
                    return new HeatmapMinMaxResponse
                    {
                        Min = min,
                        Max = max
                    };
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        public async Task<byte[]?> ObtenerImagenHeatmapAsync(
        Camara camara,
        HeatmapDataInformacionRequest heatMapDataInformacionRequest,
        CancellationToken ct = default)
        {
            try
            {
                using var client = _clientFactory.Crear(camara);

                var fechaInicio = heatMapDataInformacionRequest.FechaInicio.ToString("s");
                var fechaFinal = heatMapDataInformacionRequest.FechaFinal.ToString("s");

                var request = new RestRequest($"/ISAPI/System/Video/inputs/channels/1/heatMap/picture?starttime={fechaInicio}&endtime={fechaFinal}&statisticalModel={heatMapDataInformacionRequest.ModeloEstadistico}", Method.Get);

                var response = await client.ExecuteAsync(request, ct);

                if (!response.IsSuccessful)
                {
                    return null;
                }

                if (response.ContentType != null && response.ContentType.Contains("xml", StringComparison.OrdinalIgnoreCase))
                {
                    return null;
                }

                return response.RawBytes;
            }
            catch
            {
                return null;
            }
        }

        public class HeatmapInformacion
        {
            public bool Error { get; set; } = true;
            public string? ErrorMensaje { get; set; }
            public DateTime Fecha { get; set; }
            public bool HeatmapHabilitado { get; set; } = false;
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
