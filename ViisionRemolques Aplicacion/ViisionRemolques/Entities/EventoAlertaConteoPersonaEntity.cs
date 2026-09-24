namespace ViisionRemolques.Entities
{
    public class EventoAlertaConteoPersonaEntity
    {
        public long IdInterno { get; set; }
        public long? IdExterno { get; set; }
        public string? IPCamara { get; set; } = string.Empty;
        public string Evento { get; set; } = string.Empty;
        public string? Regiones { get; set; } = string.Empty;
        public string TotalEntradas { get; set; } = "0";
        public string TotalSalidas { get; set; } = "0";
        public string TotalPasos { get; set; } = "0";
        public string TotalDuplicados { get; set; } = "0";
        public DateTime FechaEvento { get; set; } = DateTime.Now;
        public int Prioridad { get; set; } = 5;
        public bool Sincronizado { get; set; }
        public DateTime FechaRegistro { get; set; } = DateTime.Now;
    }
}
