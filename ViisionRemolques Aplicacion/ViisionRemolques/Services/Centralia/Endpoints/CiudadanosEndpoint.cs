using System.Text.Json.Serialization;
using ViisionRemolques.Utils;

namespace ViisionRemolques.Services.Centralia.Endpoints
{
    public class CiudadanosEndpoint
    {
        private readonly CentraliaApiClientService _centraliaApiClientService;

        public CiudadanosEndpoint(CentraliaApiClientService centraliaApiClientService)
        {
            _centraliaApiClientService = centraliaApiClientService;
        }

        public async Task<Resultado<CiudadanoReportadoRespuesta>> Obtener(string? version = null)
        {
            try
            {
                return Resultado<CiudadanoReportadoRespuesta>.Ok(
                    await _centraliaApiClientService.PostAsync<CiudadanoReportadoRespuesta>("CiudadanosReportados/Obtener_CiudadanosRegistrados", new {
                        fecha_Actualizacion = version,
                    }));
            }
            catch (Exception excepcion) {

                return Resultado<CiudadanoReportadoRespuesta>.Fallo(excepcion);
            }
        }

        public async Task<Resultado<CiudadanoReportadoImagenRespuesta>> ObtenerImagenes(string? version = null)
        {
            try
            {
                return Resultado<CiudadanoReportadoImagenRespuesta>.Ok(
                    await _centraliaApiClientService.PostAsync<CiudadanoReportadoImagenRespuesta>($"/Autonomo/CiudadanoRostro/Obtener_RostrosBlacklist", new
                    {
                        fecha_Actualizacion = version,
                    }));
            }
            catch (Exception excepcion)
            {
                return Resultado<CiudadanoReportadoImagenRespuesta>.Fallo(excepcion);
            }
        }
    }

    public class CiudadanoReportadoImagenRespuesta
    {
        [JsonPropertyName("fechaGlobal")]
        public string? FechaGlobal { get; set; }

        [JsonPropertyName("novedades")]
        public bool? Novedades { get; set; } = false;

        [JsonPropertyName("datos")]
        public List<CiudadanoReportadoImagenDetalleRespuesta> Datos { get; set; } = new();
    }

    public class CiudadanoReportadoImagenDetalleRespuesta
    {
        [JsonPropertyName("idDatCiudadanoRostro")]
        public long? IdExterno { get; set; }

        [JsonPropertyName("idDatCiudadanoReportado")]
        public long? CiudadanoIdExterno { get; set; }

        [JsonPropertyName("imagenPath")]
        public string? Path { get; set; }

        [JsonPropertyName("imagenHash")]
        public string? Hash { get; set; }

        [JsonPropertyName("activo")]
        public bool? Activo { get; set; } = false;

        [JsonPropertyName("fechaCreacion")]
        public DateTime? FechaCreacion { get; set; }

        [JsonPropertyName("fechaModificacion")]
        public DateTime? FechaModificacion { get; set; }
    }

    public class CiudadanoReportadoRespuesta
    {
        [JsonPropertyName("fechaGlobal")]
        public string? FechaGlobal { get; set; }

        [JsonPropertyName("novedades")]
        public bool? Novedades { get; set; } = true;

        [JsonPropertyName("datos")]
        public List<CiudadanoReportado>? Datos { get; set; } = new();
    }

    public class CiudadanoReportado
    {
        [JsonPropertyName("idDatCiudadanoReportado")]
        public long? IdExterno { get; set; }

        [JsonPropertyName("nombre")]
        public string? Nombre { get; set; }

        [JsonPropertyName("apellidoPaterno")]
        public string? ApellidoPaterno { get; set; }

        [JsonPropertyName("apellidoMaterno")]
        public string? ApellidoMaterno { get; set; }

        [JsonPropertyName("sexo")]
        public string? Sexo { get; set; }

        [JsonPropertyName("fechaNacimiento")]
        public DateTime? FechaNacimiento { get; set; }

        [JsonPropertyName("rfc")]
        public string? RFC { get; set; }

        [JsonPropertyName("curp")]
        public string? Curp { get; set; }

        [JsonPropertyName("ife")]
        public string? IFE { get; set; }

        [JsonPropertyName("mensaje")]
        public string? Mensaje { get; set; }

        [JsonPropertyName("accion")]
        public string? Accion { get; set; }

        [JsonPropertyName("activo")]
        public bool? Activo { get; set; } = true;

        [JsonPropertyName("fechaCreacion")]
        public DateTime? FechaCreacion { get; set; } = DateTime.Now;

        [JsonPropertyName("fechaModificacion")]
        public DateTime? FechaModificacion { get; set; } = DateTime.Now;

        [JsonPropertyName("altoRiesgo")]
        public bool? AltoRiesgo { get; set; } = false;
    }
}
