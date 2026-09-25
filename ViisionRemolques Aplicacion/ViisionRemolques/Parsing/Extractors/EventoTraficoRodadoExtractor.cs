using System.Xml.Linq;
using ViisionRemolques.Enums;
using ViisionRemolques.Parsing.Models;

namespace ViisionRemolques.Parsing.Extractors
{
    public class EventoTraficoRodadoExtractor : ICameraEventSectionExtractor
    {
        public VCAModoEnum VCAModo { get; set; } = VCAModoEnum.TraficoRodado;

        private static readonly string[] EventosAplicables =
        [
            EventoTraficoRodadoEnum.ANPR,
            EventoTraficoRodadoEnum.TPSRealTime,
        ];

        public bool AplicaPara(string eventType, XDocument? doc = null) =>
            EventosAplicables.Contains(eventType, StringComparer.OrdinalIgnoreCase);

        public void Extraer(XDocument doc, EventoExtractorModelo evento)
        {
            int traficoEstadisticasVehiculos = 0;
            int traficoEstadisticasMotocicletas = 0;




            evento.EventoTraficoRodado = new EventoTraficoRodadoExtractorModelo
            {
                Matricula = doc.Buscar("//anpr/licenseplate"),
                VehiculoDosRuedas = doc.Buscar("//anpr/twowheelvehicle"),
                VehiculoTresRuedas = doc.Buscar("//anpr/threewheelvehicle"),

                TraficoEstadisticasVehiculos = 0,
                TraficoEstadisticasMotocicletas = 0
            };
        }
    }

    public class EventoTraficoRodadoExtractorModelo
    {
        public string? Matricula { get; set; }
        public string? VehiculoDosRuedas { get; set; }
        public string? VehiculoTresRuedas { get; set; }

        public int? TraficoEstadisticasVehiculos { get; set; }
        public int? TraficoEstadisticasMotocicletas { get; set; }
    }

    public static class EventoTraficoRodadoEnum
    {
        public static readonly string ANPR = "ANRP";
        public static readonly string TPSRealTime = "TPSRealTime";
    }
}
