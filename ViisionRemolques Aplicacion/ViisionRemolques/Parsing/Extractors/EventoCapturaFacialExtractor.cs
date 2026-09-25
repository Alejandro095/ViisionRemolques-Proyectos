using System.Xml.Linq;
using ViisionRemolques.Enums;
using ViisionRemolques.Parsing.Models;

namespace ViisionRemolques.Parsing.Extractors
{
    public class EventoCapturaFacialExtractor : ICameraEventSectionExtractor
    {
        public VCAModoEnum VCAModo { get; set; } = VCAModoEnum.CapturaFacial;

        private static readonly string[] EventosAplicables =
        [
            EventoCapturaFacialEnum.CapturaFacial
        ];

        public bool AplicaPara(string eventType, XDocument? doc = null) =>
            EventosAplicables.Contains(eventType, StringComparer.OrdinalIgnoreCase);

        public void Extraer(XDocument doc, EventoExtractorModelo evento)
        {
            evento.EventoCapturaFacial = new EventoCapturaFacialExtractorModelo
            {
                CoordenadasRostros = doc.BuscarXMLaJSONInnerArrayJSON("//facecapture//faces/facerect"),
            };
        }
    }

    public class EventoCapturaFacialExtractorModelo
    {
        public string? CoordenadasRostros { get; set; }
    }

    public static class EventoCapturaFacialEnum
    {
        public static readonly string CapturaFacial = "faceCapture";
    }
}
