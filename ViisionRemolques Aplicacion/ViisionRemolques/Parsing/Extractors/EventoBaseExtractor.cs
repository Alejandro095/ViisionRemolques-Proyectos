using System.Xml.Linq;
using ViisionRemolques.Enums;
using ViisionRemolques.Parsing.Models;

namespace ViisionRemolques.Parsing.Extractors
{
    /// <summary>Datos comunes a todos los eventos. Se ejecuta siempre.</summary>
    public class EventoBaseExtractor : ICameraEventSectionExtractor
    {
        public VCAModoEnum VCAModo { get; set; } = VCAModoEnum.Ninguno;

        public bool AplicaPara(string eventType, XDocument? doc = null) => true;

        public void Extraer(XDocument doc, EventoExtractorModelo evento)
        {
            evento.Evento = new EventoBaseExtractorModelo
            {
                IP = doc.Buscar("//ipaddress"),
                MAC = doc.Buscar("//macaddress"),
                EventType = doc.Buscar("//eventtype"),
                EventState = doc.Buscar("//eventstate"),
                VCAModo = VCAModoEnum.Ninguno,
            };
        }
    }

    public class EventoBaseExtractorModelo
    {
        public string? IP { get; set; }
        public string? MAC { get; set; }
        public string? EventType { get; set; }
        public string? EventState { get; set; }
        public VCAModoEnum VCAModo { get; set; }
        public DateTime Fecha = DateTime.Now;
    }
}
