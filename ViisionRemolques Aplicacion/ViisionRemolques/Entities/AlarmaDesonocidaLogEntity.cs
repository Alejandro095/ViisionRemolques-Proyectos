namespace ViisionRemolques.Entities
{
    public class AlarmaDesonocidaLogEntity
    {
        public string? IPCamara { get; set; }
        public string? ContentType { get; set; }
        public string? Evento { get; set; }
        public string Body { get; set; } = string.Empty;
        public string Motivo { get; set; } = string.Empty;
    }

}
