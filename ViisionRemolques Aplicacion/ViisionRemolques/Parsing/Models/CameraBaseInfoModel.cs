using ViisionRemolques.Enums;

namespace ViisionRemolques.Parsing.Models
{
    /// <summary>Datos presentes en cualquier evento, sea del tipo que sea.</summary>
    public class CameraBaseInfoModel
    {
        public string? IpAddress { get; set; }

        public VCAModoEnum? VCAModo { get; set; }

        /// <summary>Evento real. En un "duration" es el que viene dentro, no el envoltorio.</summary>
        public string? EventType { get; set; }
        

        /// <summary>El eventType tal cual lo mandó la cámara, sin desenvolver. Sirve para filtrar.</summary>
        public string? RawEventType { get; set; }

        public string? EventState { get; set; }
        public string? PId { get; set; }
    }
}
