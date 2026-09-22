namespace ViisionRemolques.Entities
{
    public class EventoPerimetral
    {
        public long IdInterno { get; set; }
        public long? IdExterno { get; set; }
        public string? PId { get; set; } = string.Empty;
        public string? IPCamara { get; set; } = string.Empty;
        public string Evento { get; set; } = string.Empty;
        public string? ReglaId { get; set; } = string.Empty;
        public string? TipoObjetivo { get; set; }
        public DateTime FechaEvento { get; set; } = DateTime.Now;
        public string? PathImagen { get; set; }
        public bool Sincronizado { get; set; }
        public DateTime FechaRegistro { get; set; } = DateTime.Now;
    }
}
