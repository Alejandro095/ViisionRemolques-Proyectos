using ViisionRemolques.Filters.Hangfire;
using ViisionRemolques.Repositories;
using ViisionRemolques.Services;
using ViisionRemolques.Services.Centralia.Endpoints;

namespace ViisionRemolques.Jobs
{
    [UniqueExecution]
    public class SincronizarCiudadanosJob
    {
        private readonly CiudadanosService _ciudadanosReportadosService;
        private readonly FDLibSyncService _fdLibSyncService;

        public SincronizarCiudadanosJob(CiudadanosService ciudadanosReportadosService, FDLibSyncService fdLibSyncService)
        {
            _ciudadanosReportadosService = ciudadanosReportadosService;
            _fdLibSyncService = fdLibSyncService;
        }

        public async Task Run()
        {
            // Descagar ciudadanos reportados
            var syncCiudadanos = await _ciudadanosReportadosService.SincronizarCiudadanosReportados();
            if (!syncCiudadanos.Exito) throw syncCiudadanos.ObtenerExcepcion()!;

            // Descargar registros asignados a un ciudadano reportado de imagenes
            var syncCiudadanosImagenes = await _ciudadanosReportadosService.SincronizarCiudadanosReportadosImagenes();
            if (!syncCiudadanosImagenes.Exito) throw syncCiudadanosImagenes.ObtenerExcepcion()!;

            // Procesar la tabla de estado actual de la imagenes
            var syncDiferenciasRostros = await _ciudadanosReportadosService.ProcesarDiferenciasAsync();
            if (!syncDiferenciasRostros.Exito) throw syncDiferenciasRostros.ObtenerExcepcion()!;

            // Verificar integridad contra la cámara: borra huérfanos, marca pendiente lo que la BD
            // cree sincronizado pero no está en la cámara
            var integridad = await _fdLibSyncService.VerificarIntegridadAsync();
            if (!integridad.Exito) throw integridad.ObtenerExcepcion()!;

            // Ejecutar batch de operaciones pendientes de imagenes
            var syncDiferenciasRostrosCamara = await _fdLibSyncService.SincronizarAsync();
            if (!syncDiferenciasRostrosCamara.Exito) throw syncDiferenciasRostrosCamara.ObtenerExcepcion()!;
        }
    }
}
