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

    public static class EventoSmartEnum
    {
        public static readonly string Intruciones = "fielddetection";
        public static readonly string SalidaRegion = "regionentrance";
        public static readonly string EntradaRegion = "regionentrance";
        public static readonly string CruceLinea = "linedetection";
        public static readonly string Merodeo = "loitering";
        public static readonly string Estacionamiento = "parking";
        public static readonly string MovimientoRapido = "rapidMove";
        public static readonly string PersonasReunidas = "group";
        public static readonly string EquipajeDesatendido = "unattendedBaggage";
        public static readonly string ObjectoRemovido = "attendedBaggage";
        public static readonly string VideoMotionDetection = "VMD";
    }

}
