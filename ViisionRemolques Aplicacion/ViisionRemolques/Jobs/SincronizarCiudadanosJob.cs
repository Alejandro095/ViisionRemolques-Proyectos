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
            if (!syncCiudadanos.Exito) syncCiudadanos.LanzarErrorSiFalla();

            // Descargar registros asignados a un ciudadano reportado de imagenes
            var syncCiudadanosImagenes = await _ciudadanosReportadosService.SincronizarCiudadanosReportadosImagenes();
            if (!syncCiudadanosImagenes.Exito) syncCiudadanosImagenes.LanzarErrorSiFalla();


            // Procesar la tabla de estado actual de la imagenes
            var syncDiferenciasRostros = await _ciudadanosReportadosService.ProcesarDiferenciasAsync();
            if (!syncDiferenciasRostros.Exito) syncDiferenciasRostros.LanzarErrorSiFalla();

            // Ejecutar batch de operaciones pendientes de imagenes
            var syncDiferenciasRostrosCamara = await _fdLibSyncService.SincronizarAsync();
            if (!syncDiferenciasRostrosCamara.Exito) syncDiferenciasRostrosCamara.LanzarErrorSiFalla();
        }
    }
}
