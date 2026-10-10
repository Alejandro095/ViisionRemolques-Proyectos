using System.Text.Json.Serialization;
using ViisionRemolques.Utils;

namespace ViisionRemolques.Services.Centralia.Endpoints
{
    public class CiudadanosReportadosEndpoint
    {

        //public async Task<Resultado> Obtener()
        //{

        //}

    }

    

    public class CiudadanoReportado
    {
        [JsonPropertyName("idDatCiudadanoReportado")]
        public int IdDatCiudadanoReportado { get; set; }

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
        public string? Rfc { get; set; }

        [JsonPropertyName("curp")]
        public string? Curp { get; set; }

        [JsonPropertyName("ife")]
        public string? Ife { get; set; }

        [JsonPropertyName("mensaje")]
        public string? Mensaje { get; set; }

        [JsonPropertyName("accion")]
        public string? Accion { get; set; }

        [JsonPropertyName("activo")]
        public bool Activo { get; set; }

        [JsonPropertyName("fechaCreacion")]
        public DateTime? FechaCreacion { get; set; }

        [JsonPropertyName("fechaModificacion")]
        public DateTime? FechaModificacion { get; set; }

        [JsonPropertyName("altoRiesgo")]
        public bool AltoRiesgo { get; set; }
    }
}
