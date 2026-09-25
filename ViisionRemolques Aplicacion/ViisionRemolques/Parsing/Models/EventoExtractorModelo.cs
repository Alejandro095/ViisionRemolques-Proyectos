using ViisionRemolques.Parsing.Extractors;

namespace ViisionRemolques.Parsing.Models
{
    public class EventoExtractorModelo
    {
        public EventoBaseExtractorModelo Evento { get; set; } = new();
        public EventoSmartExtractorModelo? EventoSmart { get; set; }
        public EventoRecuentoPersonasExtractorModelo? AlarmaConteoPersonas{ get; set; }
        public EventoAlarmaRecuentoPersonasExtractorModelo? EventoAlarmaRecuentoPersonas { get; set; }
        public EventoCapturaFacialExtractorModelo? EventoCapturaFacial { get; set; }
        public EventoTraficoRodadoExtractorModelo? EventoTraficoRodado { get; set; }
    }
}
