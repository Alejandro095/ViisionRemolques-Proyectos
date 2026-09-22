namespace ViisionRemolques.Parsing.Models
{
    /// <summary>Datos del objetivo detectado y de la regla que disparó el evento.</summary>
    public class EventoSmartModel
    {
        public string? TargetID { get; set; }
        public string? RegionID { get; set; }
        public string? DetectionTarget { get; set; }
    }
}
