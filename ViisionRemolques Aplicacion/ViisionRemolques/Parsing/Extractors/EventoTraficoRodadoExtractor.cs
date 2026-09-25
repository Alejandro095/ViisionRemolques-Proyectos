using System.Xml.Linq;
using ViisionRemolques.Enums;
using ViisionRemolques.Parsing.Models;

namespace ViisionRemolques.Parsing.Extractors
{
    public class EventoTraficoRodadoExtractor : ICameraEventSectionExtractor
    {
        public VCAModoEnum VCAModo { get; set; } = VCAModoEnum.EventoSmart;

        private static readonly string[] EventosAplicables =
        [
            EventoTraficoRodadoEnum.ANPR,
            EventoTraficoRodadoEnum.TPSRealTime,
        ];

        public bool AplicaPara(string eventType, XDocument? doc = null) =>
            EventosAplicables.Contains(eventType, StringComparer.OrdinalIgnoreCase);

        public void Extraer(XDocument doc, EventoExtractorModelo evento)
        {
            evento.EventoTraficoRodado = new EventoTraficoRodadoExtractorModelo
            {
                Matricula = doc.Buscar("//anpr/licenseplate"),
                VehiculoDosRuedas = doc.Buscar("//anpr/twowheelvehicle"),
                VehiculoTresRuedas = doc.Buscar("//anpr/threewheelvehicle"),


            };
        }
    }

    public class EventoTraficoRodadoExtractorModelo
    {
        public string? Matricula { get; set; }
        public string? VehiculoDosRuedas { get; set; }
        public string? VehiculoTresRuedas { get; set; }
    }

    public static class EventoTraficoRodadoEnum
    {
        public static readonly string ANPR = "ANRP";
        public static readonly string TPSRealTime = "TPSRealTime";
    }
}
