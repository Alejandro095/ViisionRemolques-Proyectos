using ViisionRemolques.Repositories;
using ViisionRemolques.Services.ISAPI;
using ViisionRemolques.Utils;

namespace ViisionRemolques.Services
{
    public class FDLibSyncService
    {
        private const long IdInternoCamara = 1;

        private readonly FDLibSyncStateRepository _fdLibSyncStateRepository;
        private readonly FDLibService _fdLibService;
        private readonly CamaraRepository _camaraRepository;

        public FDLibSyncService(
            FDLibSyncStateRepository fdLibSyncStateRepository,
            FDLibService fdLibService,
            CamaraRepository camaraRepository)
        {
            _fdLibSyncStateRepository = fdLibSyncStateRepository;
            _fdLibService = fdLibService;
            _camaraRepository = camaraRepository;
        }

        public async Task<Resultado> SincronizarAsync(CancellationToken ct = default)
        {
            var camara = await _camaraRepository.ObtenerPorIdInternoAsync(IdInternoCamara);
            if (camara is null)
                return Resultado.Fallo($"No se encontró ninguna cámara con el id '{IdInternoCamara}'.");

            var fdidResultado = await _fdLibService.IntentarObtenerFDIDLibreriaAsync(camara, ct);
            if (!fdidResultado.Exito) return Resultado.Fallo(fdidResultado.Error!);
            if (fdidResultado.Valor is null) return Resultado.Fallo("La biblioteca facial no existe en la cámara todavía.");

            var fdid = fdidResultado.Valor;

            var diffResultado = await _fdLibSyncStateRepository.ProcesarDiferencias();
            if (!diffResultado.Exito) return Resultado.Fallo(diffResultado.Error!);

            var pendientesResultado = await _fdLibSyncStateRepository.ObtenerDiferencias();
            if (!pendientesResultado.Exito) return Resultado.Fallo(pendientesResultado.Error!);

            foreach (var fila in pendientesResultado.Valor!)
            {
                switch (fila.Accion)
                {
                    case "Borrar":
                        await ProcesarBorrarAsync(camara, fdid, fila, ct);
                        break;

                    case "Insertar":
                        await ProcesarInsertarAsync(camara, fdid, fila, ct);
                        break;

                    case "Actualizar":
                        await ProcesarActualizarAsync(camara, fdid, fila, ct);
                        break;
                }
            }

            await _fdLibService.ModelarPendientesAsync(camara, fdid, ct);

            return Resultado.Ok();
        }

        private async Task ProcesarBorrarAsync(CamaraEntity camara, string fdid, FDLibSyncStateRepository.FDLibPendiente fila, CancellationToken ct)
        {
            if (fila.Pid is null)
            {
                await _fdLibSyncStateRepository.EliminarEstadoCamaraAsync(fila.ImagenIdExterno);
                return;
            }

            var resultado = await _fdLibService.BorrarImagenAsync(camara, fdid, fila.Pid, ct);

            if (resultado.Exito)
                await _fdLibSyncStateRepository.EliminarEstadoCamaraAsync(fila.ImagenIdExterno);
            else
                await _fdLibSyncStateRepository.MarcarErrorCamaraAsync(
                    new FDLibSyncStateRepository.FDLibError(fila.ImagenIdExterno, resultado.Error ?? "Error desconocido al borrar."));
        }

        private async Task ProcesarInsertarAsync(CamaraEntity camara, string fdid, FDLibSyncStateRepository.FDLibPendiente fila, CancellationToken ct)
        {
            if (fila.RutaArchivoLocal is null || fila.Hash is null)
                return; // se re-clasifica solo en el próximo diff

            var subida = await SubirImagenAsync(camara, fdid, fila, ct);
            if (subida is null) return;

            await _fdLibSyncStateRepository.MarcarSincronizadoAsync(
                new FDLibSyncStateRepository.FDLibExito(fila.ImagenIdExterno, subida, fila.Hash));
        }

        private async Task ProcesarActualizarAsync(CamaraEntity camara, string fdid, FDLibSyncStateRepository.FDLibPendiente fila, CancellationToken ct)
        {
            if (fila.RutaArchivoLocal is null || fila.Hash is null)
                return;

            if (fila.Pid is not null)
            {
                var borrado = await _fdLibService.BorrarImagenAsync(camara, fdid, fila.Pid, ct);
                if (!borrado.Exito)
                {
                    await _fdLibSyncStateRepository.MarcarErrorCamaraAsync(
                        new FDLibSyncStateRepository.FDLibError(fila.ImagenIdExterno, borrado.Error ?? "Error al borrar PID viejo antes de actualizar."));
                    return;
                }
            }

            var subida = await SubirImagenAsync(camara, fdid, fila, ct);
            if (subida is null) return;

            await _fdLibSyncStateRepository.MarcarSincronizadoAsync(
                new FDLibSyncStateRepository.FDLibExito(fila.ImagenIdExterno, subida, fila.Hash));
        }

        private async Task<string?> SubirImagenAsync(CamaraEntity camara, string fdid, FDLibSyncStateRepository.FDLibPendiente fila, CancellationToken ct)
        {
            byte[] imagenBytes;
            try
            {
                imagenBytes = await File.ReadAllBytesAsync(fila.RutaArchivoLocal!, ct);
            }
            catch (Exception ex)
            {
                await _fdLibSyncStateRepository.MarcarErrorCamaraAsync(
                    new FDLibSyncStateRepository.FDLibError(fila.ImagenIdExterno, $"No se pudo leer el archivo local: {ex.Message}"));
                return null;
            }

            var personId = $"{fila.CiudadanoIdExterno}v{fila.ImagenIdExterno}";
            var nombre = personId;
           

            var request = new FDLibService.InsertarImagenRequest(fdid, personId, nombre, imagenBytes);
            var resultado = await _fdLibService.InsertarImagenAsync(camara, request, ct);

            if (!resultado.Exito)
            {
                await _fdLibSyncStateRepository.MarcarErrorCamaraAsync(
                    new FDLibSyncStateRepository.FDLibError(fila.ImagenIdExterno, resultado.Error ?? "Error desconocido al subir."));
                return null;
            }

            return resultado.Valor;
        }
    }
}
