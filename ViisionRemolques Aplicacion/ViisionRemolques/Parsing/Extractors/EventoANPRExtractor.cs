using System.Xml.Linq;
using ViisionRemolques.Enums;
using ViisionRemolques.Parsing.Models;

namespace ViisionRemolques.Parsing.Extractors
{
    public class EventoANPRExtractor : ICameraEventSectionExtractor
    {
        public VCAModoEnum VCAModo { get; set; } = VCAModoEnum.ANPR;

        private static readonly string[] EventosAplicables =
        [
            EventoANPREnum.ANPR,
        ];

        public bool AplicaPara(string eventType, XDocument? doc = null) =>
            EventosAplicables.Contains(eventType, StringComparer.OrdinalIgnoreCase);

        public void Extraer(XDocument doc, EventoExtractorModelo evento)
        {
            string? VelocidadVehiculo = doc.Buscar("//anpr/vehicleinfo/speed");

            evento.EventoANPR = new EventoANPRExtractorModelo
            {
                Matricula = doc.Buscar("//anpr/licenseplate"),
                VehiculoDosRuedas = doc.Buscar("//anpr/twowheelvehicle"),
                VehiculoTresRuedas = doc.Buscar("//anpr/threewheelvehicle"),

                VehiculoColor = doc.Buscar("//anpr/vehicleinfo/color"),
                VehiculoTipo = doc.Buscar("//anpr/vehicletype"),

                Direccion = doc.Buscar("//anpr/direction"),
                NumeroCarril = doc.Buscar("//anpr/line"),

                NombreLista = doc.Buscar("//anpr/vehiclelistname"),

                Radar = !string.IsNullOrEmpty(VelocidadVehiculo),
                Velocidad = VelocidadVehiculo,
            };
        }
    }

    public class EventoANPRExtractorModelo
    {
        public string? Matricula { get; set; }
        public string? VehiculoDosRuedas { get; set; }
        public string? VehiculoTresRuedas { get; set; }

        public string? VehiculoTipo { get; set; }
        public string? VehiculoColor { get; set; }
        public string? Direccion { get; set; }
        public string? NumeroCarril { get; set; }
        public string? NombreLista { get; set; }

        public bool Radar { get; set; }
        public string? Velocidad { get; set; }
    }

    public static class EventoANPREnum
    {
        public static readonly string ANPR = "ANPR";
    }
}
