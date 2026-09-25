using ViisionRemolques.Parsing.Extractors;

namespace ViisionRemolques.Parsing.Models
{
    /// <summary>
    /// Evento ya interpretado. Se compone por secciones: cada extractor rellena la suya,
    /// y las que no apliquen al tipo de evento se quedan en null.
    /// </summary>
    public class CameraEventModel
    {
        public CameraBaseInfoModel BaseInfo { get; set; } = new();

        public EventoSmartModel? EventoSmart { get; set; }
        public AlarmaConteoPersonasModel? AlarmaConteoPersonas{ get; set; }


        public EventoAlarmaRecuentoPersonasExtractorModelo? EventoAlarmaRecuentoPersonas { get; set; }
    }
}
