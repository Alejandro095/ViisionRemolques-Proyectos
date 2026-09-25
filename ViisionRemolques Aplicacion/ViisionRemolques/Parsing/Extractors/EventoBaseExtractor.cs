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
                IpAddress = doc.Buscar("//ipaddress"),
                EventType = doc.Buscar("//eventtype"),
                EventState = doc.Buscar("//eventstate"),
                VCAModo = VCAModoEnum.Ninguno,
            };
        }
    }

    public class EventoBaseExtractorModelo
    {
        public string? IpAddress { get; set; }
        public string? EventType { get; set; }
        public string? EventState { get; set; }
        public string? PId { get; set; }
        public VCAModoEnum? VCAModo { get; set; }
    }
}
