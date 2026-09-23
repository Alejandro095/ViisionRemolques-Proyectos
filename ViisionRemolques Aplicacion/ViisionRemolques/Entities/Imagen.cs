namespace ViisionRemolques.Entities
{
    public class Imagen
    {
        public long IdInterno { get; set; }
        public string OrigenTabla { get; set; } = string.Empty;
        public long OrigenIdInterno { get; set; }
        public string PathImagen { get; set; } = string.Empty;
        public bool Sincronizado { get; set; }
        public DateTime FechaCreacion { get; set; }
    }
}
