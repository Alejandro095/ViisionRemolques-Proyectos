using ViisionRemolques.Repositories;
using ViisionRemolques.Services.ISAPI;
using ViisionRemolques.Utils;
using static ViisionRemolques.Repositories.FDLibSyncStateRepository;
using static ViisionRemolques.Services.ISAPI.FDLibService;

namespace ViisionRemolques.Services
{
    public class FDLibSyncService
    {
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
            var camara = await _camaraRepository.ObtenerCamaraCompatibleReconocimientoFacial();
            if (camara is null)
                return Resultado.Fallo($"No se encontró ninguna cámara compatible con reconocimiento facial.");

            var fdidResultado = await _fdLibService.ObtenerCrearFidAsync(camara, ct);

            if (!fdidResultado.Exito) return Resultado.Fallo(fdidResultado.Error!);

            var fdid = fdidResultado.Valor!;

            var resultadoDiferencias = await _fdLibSyncStateRepository.ObtenerDiferenciasProcesadas();

            if (!resultadoDiferencias.Exito) return Resultado.Fallo(resultadoDiferencias.Error!);

            foreach (var diferencia in resultadoDiferencias.Valor!)
            {
                var certificateNumber = $"{diferencia.CiudadanoIdExterno}v{diferencia.ImagenIdExterno}";

                var busqueda = await _fdLibService.BuscarPorCertificateNumberAsync(camara, fdid, certificateNumber, ct);

                if (!busqueda.Exito) // Esto solo deberia de fallar si no hay conexion a la camara
                {
                    await _fdLibSyncStateRepository.MarcarErrorCamaraAsync(
                        new FDLibError(diferencia.ImagenIdExterno, busqueda.Error ?? "Error al buscar el PID en la cámara."));
                    continue;
                }

                string? pid = busqueda.Valor;

                switch (diferencia.Accion)
                {
                    case "Borrar":
                        await ProcesarBorrarAsync(camara, fdid, diferencia, pid, ct);
                        break;

                    case "Insertar":
                    case "Actualizar":
                        await ProcesarSubidaAsync(camara, fdid, diferencia, pid, certificateNumber, ct);
                        break;
                }
            }

            await _fdLibService.ModelarPendientesAsync(camara, fdid, ct);

            return Resultado.Ok();
        }

        public async Task<Resultado> VerificarIntegridadAsync(CancellationToken ct = default)
        {
            var camara = await _camaraRepository.ObtenerCamaraCompatibleReconocimientoFacial();
            if (camara is null)
                return Resultado.Fallo("No se encontró ninguna cámara compatible con reconocimiento facial.");

            var fdidResultado = await _fdLibService.ObtenerCrearFidAsync(camara, ct);
            if (!fdidResultado.Exito) return Resultado.Fallo(fdidResultado.Error!);

            var fdid = fdidResultado.Valor!;

            var registrosCamaraResultado = await _fdLibService.ConsultarRegistrosAsync(camara, fdid, ct);
            if (!registrosCamaraResultado.Exito) return Resultado.Fallo(registrosCamaraResultado.Error!);

            var sincronizadosLocalResultado = await _fdLibSyncStateRepository.ObtenerSincronizadosAsync();
            if (!sincronizadosLocalResultado.Exito) return Resultado.Fallo(sincronizadosLocalResultado.Error!);

            var registrosCamara = registrosCamaraResultado.Valor!.ToList();

            // Si 'certificatenumber' es nulo, es un registro invalido asi hay que borrarlo 
            foreach (var registro in registrosCamara.Where(r => string.IsNullOrWhiteSpace(r.CertificateNumber)))
            {
                await _fdLibService.BorrarImagenAsync(camara, fdid, registro.Pid, ct);
            }

            var gruposPorCertificateNumber = registrosCamara
                .Where(r => !string.IsNullOrWhiteSpace(r.CertificateNumber))
                .GroupBy(r => r.CertificateNumber)
                .ToList();

            // El campo certificateNumber no es único. Si hay clones no se puede confiar en cuál es el
            // correcto -> se borran todos, sin excepción.
            foreach (var grupo in gruposPorCertificateNumber.Where(g => g.Count() > 1))
            {
                foreach (var registro in grupo)
                    await _fdLibService.BorrarImagenAsync(camara, fdid, registro.Pid, ct);
            }

            var registrosPorCertificateNumber = gruposPorCertificateNumber
                .Where(g => g.Count() == 1)
                .ToDictionary(g => g.Key, g => g.Single());

            var esperadosLocal = sincronizadosLocalResultado.Valor!
                .ToDictionary(f => $"{f.CiudadanoIdExterno}v{f.ImagenIdExterno}", f => f.ImagenIdExterno);

            // Faltantes: la BD cree que está sincronizado, pero no está en la cámara -> se borra el registro local.
            foreach (var (certificateNumber, imagenIdExterno) in esperadosLocal)
            {
                if (registrosPorCertificateNumber.ContainsKey(certificateNumber)) continue;

                await _fdLibSyncStateRepository.EliminarEstadoCamaraAsync(imagenIdExterno);
            }

            // Huérfanos: existe en la cámara pero la BD no tiene ningún rastro -> se borra de la cámara.
            foreach (var (certificateNumber, registro) in registrosPorCertificateNumber)
            {
                if (esperadosLocal.ContainsKey(certificateNumber)) continue;

                await _fdLibService.BorrarImagenAsync(camara, fdid, registro.Pid, ct);
            }

            return Resultado.Ok();
        }

        private async Task ProcesarBorrarAsync(CamaraEntity camara, string fdid, FDLibPendiente fila, string? pid, CancellationToken ct)
        {
            if (pid is null)
            {
                // Nunca se sincronizó, o ya no existe en la cámara (borrado manual, librería recreada, etc.)
                await _fdLibSyncStateRepository.EliminarEstadoCamaraAsync(fila.ImagenIdExterno);
                return;
            }

            var resultado = await _fdLibService.BorrarImagenAsync(camara, fdid, pid, ct);

            if (resultado.Exito)
                await _fdLibSyncStateRepository.EliminarEstadoCamaraAsync(fila.ImagenIdExterno);
            else
                await _fdLibSyncStateRepository.MarcarErrorCamaraAsync(
                    new FDLibError(fila.ImagenIdExterno, resultado.Error ?? "Error desconocido al borrar."));
        }

        private async Task ProcesarSubidaAsync(CamaraEntity camara, string fdid, FDLibPendiente fila, string? pid, string certificateNumber, CancellationToken ct)
        {
            if (fila.RutaArchivoLocal is null || fila.Hash is null)
                return; // se re-clasifica solo en el próximo diff

            // Si el pid no es null, es porque ya hay un regristo con mismo certificateNumber de esta peticion
            if (pid is not null)
            {
                var borrado = await _fdLibService.BorrarImagenAsync(camara, fdid, pid, ct);
                // Esto solo deberia de fallar si no hay conexion a la camara
                if (!borrado.Exito)
                {
                    await _fdLibSyncStateRepository.MarcarErrorCamaraAsync(
                        new FDLibError(fila.ImagenIdExterno, borrado.Error ?? "Error al borrar PID viejo antes de subir."));
                    return;
                }
            }

            var subida = await SubirImagenAsync(camara, fdid, fila, certificateNumber, ct);

            if (subida is null) return;

            await _fdLibSyncStateRepository.MarcarSincronizadoAsync(
                new FDLibExito(fila.ImagenIdExterno, fila.Hash));
        }

        private async Task<string?> SubirImagenAsync(CamaraEntity camara, string fdid, FDLibPendiente fila, string certificateNumber, CancellationToken ct)
        {
            byte[] imagenBytes;
            try
            {
                imagenBytes = await File.ReadAllBytesAsync(fila.RutaArchivoLocal!, ct);
            }
            catch (Exception ex)
            {
                await _fdLibSyncStateRepository.MarcarErrorCamaraAsync(
                    new FDLibError(fila.ImagenIdExterno, $"No se pudo leer el archivo local: {ex.Message}"));
                return null;
            }

            var resultado = await _fdLibService.InsertarImagenAsync(camara, 
                new InsertarImagenRequest(fdid, certificateNumber, imagenBytes), ct);

            if (!resultado.Exito)
            {
                await _fdLibSyncStateRepository.MarcarErrorCamaraAsync(
                    new FDLibError(fila.ImagenIdExterno, resultado.Error ?? "Error desconocido al subir."));
                return null;
            }

            return resultado.Valor;
        }
    }
}
