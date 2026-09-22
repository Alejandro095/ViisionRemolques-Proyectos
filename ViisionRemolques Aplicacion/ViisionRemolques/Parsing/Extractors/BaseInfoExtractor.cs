using System.Xml.Linq;
using ViisionRemolques.Enums;
using ViisionRemolques.Parsing.Models;

namespace ViisionRemolques.Parsing.Extractors
{
    /// <summary>Datos comunes a todos los eventos. Se ejecuta siempre.</summary>
    public class BaseInfoExtractor : ICameraEventSectionExtractor
    {
        public VCAModoEnum VCAModo { get; set; } = VCAModoEnum.Ninguno;

        public bool AplicaPara(string eventType, XDocument? doc = null) => true;

        public void Extraer(XDocument doc, CameraEventModel evento)
        {
            evento.BaseInfo = new CameraBaseInfoModel
            {
                IpAddress = doc.Buscar("//ipaddress", "//ipv4address", "//srcaddress"),
                // El evento real de un "duration" viene dentro; por eso esta regla va primero.
                EventType = doc.Buscar("//durationlist/duration/relationevent", "//eventtype"),
                RawEventType = doc.Buscar("//eventtype"),
                EventState = doc.Buscar("//eventstate"),
                PId = doc.Buscar("//pid", "//picid"),
                VCAModo = VCAModoEnum.Ninguno,
            };
        }
    }
}
