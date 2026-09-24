namespace ViisionRemolques.Services
{
    public class AlmacenamientoImagenesService
    {

        private readonly string _basePath;

        public AlmacenamientoImagenesService(IWebHostEnvironment environment)
        {

            _basePath = Path.Combine(environment.ContentRootPath, "Archivos/Imagenes");
        }

        public async Task<List<string>> Guardar(List<byte[]> imagenes, string extension = ".jpg")
        {
            var rutasGuardadas = new List<string>();

            if (imagenes == null || imagenes.Count == 0)
                return rutasGuardadas;

            if (!extension.StartsWith("."))
            {
                extension = "." + extension;
            }

            var hoy = DateTime.UtcNow;

            // Construir carpeta con jerarquía: {ContentRootPath}/{subcarpeta}/{yyyy}/{MM}/{dd}
            var carpetaDestino = Path.Combine(
                _basePath,
                hoy.ToString("yyyy"),
                hoy.ToString("MM"),
                hoy.ToString("dd")
            );

            if (!Directory.Exists(carpetaDestino))
            {
                Directory.CreateDirectory(carpetaDestino);
            }

            foreach (var imagenBytes in imagenes)
            {
                if (imagenBytes == null || imagenBytes.Length == 0)
                    continue;

                var nombreArchivo = $"{Guid.NewGuid():N}{extension}";
                var pathCompleto = Path.Combine(carpetaDestino, nombreArchivo);

                await File.WriteAllBytesAsync(pathCompleto, imagenBytes);
                rutasGuardadas.Add(pathCompleto);
            }

            return rutasGuardadas;
        }
    }
}
