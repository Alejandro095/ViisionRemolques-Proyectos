using System.Xml.Linq;
using ViisionRemolques.Enums;
using ViisionRemolques.Parsing.Models;

namespace ViisionRemolques.Parsing.Extractors
{
    public class EventoSmartExtractor : ICameraEventSectionExtractor
    {
        public VCAModoEnum VCAModo { get; set; } = VCAModoEnum.EventoSmart;

        private static readonly string[] EventosAplicables =
        [
            "fielddetection",
            "regionentrance",
            "regionexiting",
            "linedetection",
            "unattendedBaggage",
            "attendedBaggage"
        ];

        public bool AplicaPara(string eventType, XDocument? doc = null) =>
            EventosAplicables.Contains(eventType, StringComparer.OrdinalIgnoreCase);

        public void Extraer(XDocument doc, CameraEventModel evento)
        {
            evento.EventoSmart = new EventoSmartModel
            {
                TargetID = doc.Buscar("//targetid", "//objectid"),
                RegionID = doc.Buscar("//detectionregionentry/regionid", "//regionid", "//ruleid"),
                DetectionTarget = doc.Buscar("//detectiontarget", "//targettype")
            };
        }
    }
}
