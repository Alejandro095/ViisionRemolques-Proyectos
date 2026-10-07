using System.Xml.Linq;
using System.Xml.XPath;
using ViisionRemolques.Enums;
using ViisionRemolques.Parsing.Models;

namespace ViisionRemolques.Parsing.Extractors
{    
    public class EventoSmartExtractor : ICameraEventSectionExtractor
    {
        public VCAModoEnum VCAModo { get; set; } = VCAModoEnum.EventoSmart;

        private static readonly string[] EventosAplicables =
        [
            EventoSmartEnum.Intruciones,
            EventoSmartEnum.EntradaRegion,
            EventoSmartEnum.SalidaRegion,
            EventoSmartEnum.CruceLinea,
            EventoSmartEnum.Merodeo,
            EventoSmartEnum.Estacionamiento,
            EventoSmartEnum.MovimientoRapido,
            EventoSmartEnum.PersonasReunidas,
            EventoSmartEnum.EquipajeDesatendido,
            EventoSmartEnum.ObjectoRemovido,
            EventoSmartEnum.VideoMotionDetection,
            //"mixedTargetDetection", // Evento combinado ??? Verificar Actualizacion: Al parecer es un evento de IP camara reconocimiento facial
        ];

        public bool AplicaPara(string eventType, XDocument? doc = null) =>
            EventosAplicables.Contains(eventType, StringComparer.OrdinalIgnoreCase);

        public void Extraer(XDocument doc, EventoExtractorModelo evento)
        {
            var nodos = doc.XPathSelectElements("//detectionregionentry");

            foreach (var nodo in nodos)
            {
                evento.EventosSmart.Add(new EventoSmartExtractorModelo
                {
                    RegionId = nodo.Buscar("./regionid"),
                    ObjetivoDetectadoTipo = nodo.Buscar("./detectiontarget", "./targettype"),
                    RegionCoordenadas = nodo.BuscarXMLaJSONInnerArrayJSON("./regioncoordinateslist/regioncoordinates") ,                    
                });
            }
        }
    }

    public class EventoSmartExtractorModelo
    {
        public string? RegionId { get; set; }
        public string? ObjetivoDetectadoTipo { get; set; }
        public string? RegionCoordenadas { get; set; }
    }

    public static class EventoSmartEnum
    {
        public const string Intruciones = "fielddetection";
        public const string SalidaRegion = "regionexiting";
        public const string EntradaRegion = "regionentrance";
        public const string CruceLinea = "linedetection";
        public const string Merodeo = "loitering";
        public const string Estacionamiento = "parking";
        public const string MovimientoRapido = "rapidMove";
        public const string PersonasReunidas = "group";
        public const string EquipajeDesatendido = "unattendedBaggage";
        public const string ObjectoRemovido = "attendedBaggage";
        public const string VideoMotionDetection = "VMD";
    }

}
