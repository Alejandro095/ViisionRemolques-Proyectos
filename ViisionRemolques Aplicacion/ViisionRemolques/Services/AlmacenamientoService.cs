using ViisionRemolques.Utils;

namespace ViisionRemolques.Services
{
    public class AlmacenamientoService
    {

        private readonly PathsService _pathsService;

        public AlmacenamientoService(PathsService pathsService)
        {

            _pathsService = pathsService;
        }

        public async Task<List<string>> GuardarImagen(List<byte[]> imagenes, string extension = ".jpg", CancellationToken cancellationToken = default)
        {
            var rutasGuardadas = new List<string>();

            if (imagenes == null || imagenes.Count == 0)
                return rutasGuardadas;

            if (!extension.StartsWith("."))
            {
                extension = "." + extension;
            }

            var hoy = DateTime.UtcNow;

            var carpetaDestino = Path.Combine(
                _pathsService.GetImagenesPath(),
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

                await File.WriteAllBytesAsync(pathCompleto, imagenBytes, cancellationToken: cancellationToken);
                rutasGuardadas.Add(pathCompleto);
            }

            return rutasGuardadas;
        }
    }
}
