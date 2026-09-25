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
            "ANPR"
        ];

        public bool AplicaPara(string eventType, XDocument? doc = null) =>
            EventosAplicables.Contains(eventType, StringComparer.OrdinalIgnoreCase);

        public void Extraer(XDocument doc, EventoExtractorModelo evento)
        {
            evento.EventoTraficoRodado = new EventoTraficoRodadoExtractorModelo
            {
                Matricula = doc.Buscar("//anpr/licenseplate"),
            };
        }
    }

    public class EventoTraficoRodadoExtractorModelo
    {
        public string? Matricula { get; set; }
    }
}
