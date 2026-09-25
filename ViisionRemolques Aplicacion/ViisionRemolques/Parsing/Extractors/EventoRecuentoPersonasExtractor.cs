using System.Xml.Linq;
using ViisionRemolques.Enums;
using ViisionRemolques.Parsing.Models;

namespace ViisionRemolques.Parsing.Extractors
{
    public class EventoRecuentoPersonasExtractor : ICameraEventSectionExtractor
    {
        public VCAModoEnum VCAModo { get; set; } = VCAModoEnum.RecuentoPersonas;

        private static readonly string[] EventosAplicables =
        [
            "peoplecounting"
        ];

        public bool AplicaPara(string eventType, XDocument? doc = null) =>
            EventosAplicables.Contains(eventType, StringComparer.OrdinalIgnoreCase);

        public void Extraer(XDocument doc, EventoExtractorModelo evento)
        {
            evento.AlarmaConteoPersonas = new EventoRecuentoPersonasExtractorModelo
            {                
                TotalEntradas = doc.Buscar("//peoplecounting/enter"),
                TotalSalidas = doc.Buscar("//peoplecounting/exit"),
                TotalPasos = doc.Buscar("//peoplecounting/pass"),
                TotalDuplicados = doc.Buscar("//peoplecounting/duplicatepeople"),

                Regiones = doc.BuscarInnerArrayJson("//regionlist"),
            };
        }
    }

    public class EventoRecuentoPersonasExtractorModelo
    {
        public string? Regiones { get; set; }
        public string? TotalEntradas { get; set; }
        public string? TotalSalidas { get; set; }
        public string? TotalPasos { get; set; }
        public string? TotalDuplicados { get; set; }

    }
}
