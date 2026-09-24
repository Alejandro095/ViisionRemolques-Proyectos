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
                IpAddress = doc.Buscar("//ipaddress"),
                EventType = doc.Buscar("//eventtype"),
                EventState = doc.Buscar("//eventstate"),
                VCAModo = VCAModoEnum.Ninguno,
            };
        }
    }
}
