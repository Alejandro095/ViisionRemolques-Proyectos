using ViisionRemolques.Enums;

namespace ViisionRemolques.Parsing.Models
{
    public class CameraBaseInfoModel
    {
        public string? IpAddress { get; set; }
        public string? EventType { get; set; }
        public string? EventState { get; set; }
        public string? PId { get; set; }
        public VCAModoEnum? VCAModo { get; set; }
    }
}
