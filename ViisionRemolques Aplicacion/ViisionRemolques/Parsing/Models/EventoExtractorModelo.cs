using ViisionRemolques.Parsing.Extractors;

namespace ViisionRemolques.Parsing.Models
{
    public class EventoExtractorModelo
    {
        public EventoBaseExtractorModelo Evento { get; set; } = new();
        public EventoANPRExtractorModelo? EventoANPR { get; set; }
        public EventoCapturaFacialExtractorModelo? EventoCapturaFacial { get; set; }
        public EventoTraficoRodadoExtractorModelo? EventoTraficoRodado { get; set; }
        public List<EventoAlarmaRecuentoPersonasExtractorModelo> EventosAlarmaRecuentoPersonas = new List<EventoAlarmaRecuentoPersonasExtractorModelo>();
        public List<EventoArmadoPistaPersonaExtractorModelo> EventosArmadoPistaPersona = new List<EventoArmadoPistaPersonaExtractorModelo>();
        public List<EventoDeteccionTipoMultiobjectivoExtractorModelo> EventosDeteccionTipoMultiobjectivo = new List<EventoDeteccionTipoMultiobjectivoExtractorModelo>();
        public List<EventoRecuentoPersonasExtractorModelo> EventosRecuentoPersonas = new List<EventoRecuentoPersonasExtractorModelo>();
        public List<EventoSmartExtractorModelo> EventosSmart = new List<EventoSmartExtractorModelo>();        
    }
}
