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
            "VMD", // Video Motion Detection, no es modo de IA es solo detecion de moviento por PDI
            //"mixedTargetDetection", // Evento combinado ??? Verificar Actualizacion: Al parecer es un evento de IP camara reconocimiento facial
        ];

        public bool AplicaPara(string eventType, XDocument? doc = null) =>
            EventosAplicables.Contains(eventType, StringComparer.OrdinalIgnoreCase);

        public void Extraer(XDocument doc, EventoExtractorModelo evento)
        {
            evento.EventoSmart = new EventoSmartExtractorModelo
            {
                RegionCoordenadas = doc.BuscarInnerArrayJson("//detectionregionentry/regioncoordinateslist"),
                RegionID = doc.Buscar("//detectionregionentry/regionid"),
                ObjetivoDetectadoTipo = doc.Buscar("//detectiontarget", "//targettype")
            };
        }
    }

    public class EventoSmartExtractorModelo
    {
        public string? RegionCoordenadas { get; set; }
        public string? RegionID { get; set; }
        public string? ObjetivoDetectadoTipo { get; set; }
    }
}
