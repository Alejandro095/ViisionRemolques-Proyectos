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
            "fielddetection", // Intrusiones (Entran a una area + Permanencer X tiempo)
            "regionentrance", // Entran a una area
            "regionexiting", // Salen de una area
            "linedetection", // Cruce de linea
            "loitering", // Merodeo
            "parking", // Aparcamiento
            "rapidMove", // Moviento rapido
            "group", // Personas reunidas
            "unattendedBaggage", // Equipaje desatendido
            "attendedBaggage", // Eliminacion de objetos
            //"VMD", // Video Motion Detection, no es modo de IA es solo detecion de moviento por PDI
            "mixedTargetDetection", // Evento combinado ??? Verificar 
        ];

        public bool AplicaPara(string eventType, XDocument? doc = null) =>
            EventosAplicables.Contains(eventType, StringComparer.OrdinalIgnoreCase);

        public void Extraer(XDocument doc, CameraEventModel evento)
        {
            evento.EventoSmart = new EventoSmartModel
            {
                RegionCoordinatesList = doc.BuscarInnerJson("//detectionregionentry/regioncoordinateslist"),
                RegionID = doc.Buscar("//detectionregionentry/regionid"),
                DetectionTarget = doc.Buscar("//detectiontarget", "//targettype")
            };
        }
    }
}
