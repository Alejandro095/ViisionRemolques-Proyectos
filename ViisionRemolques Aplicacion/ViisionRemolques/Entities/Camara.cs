namespace ViisionRemolques.Entities
{
    public class Camara
    {
        public long IdInterno { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string Modelo { get; set; } = string.Empty;
        public string Go2Rtc { get; set; } = string.Empty;
        public bool Activo { get; set; }
        public string? IP { get; set; }

        // Digest
        public string? DigestUsuario { get; set; }
        public string? DigestContrasena { get; set; }

        public bool SoportaPtz { get; set; }
        public bool SoportaAudioBidireccional { get; set; }

        // Eventos Smart
        public bool EventoSmartDeteccionIntrusiones { get; set; }
        public bool EventoSmartDeteccionCruceLinea { get; set; }
        public bool EventoSmartDeteccionEntradaArea { get; set; }
        public bool EventoSmartDeteccionSalidaArea { get; set; }
        public bool EventoSmartEventoCombinado { get; set; }
    }
}
