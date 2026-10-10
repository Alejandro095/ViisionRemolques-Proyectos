using ViisionRemolques.Filters.Hangfire;
using ViisionRemolques.Repositories;
using ViisionRemolques.Services.Centralia.Endpoints;

namespace ViisionRemolques.Jobs
{
    [UniqueExecution]
    public class SincronizarCiudadanosJob
    {
        private readonly CiudadanosReportadosEndpoint _ciudadanosReportadosEndpoint;
        private readonly CiudadanosRepository _ciudadanosRepository;

        public SincronizarCiudadanosJob(CiudadanosReportadosEndpoint ciudadanosReportadosEndpoint, CiudadanosRepository ciudadanosRepository)
        {
            _ciudadanosReportadosEndpoint = ciudadanosReportadosEndpoint;
            _ciudadanosRepository = ciudadanosRepository;
        }

        public async Task Run()
        {
            var ciudadanos =  await _ciudadanosReportadosEndpoint.Obtener();

            ciudadanos.LanzarErrorSiFalla();

            var insert = await _ciudadanosRepository.Actualizar(ciudadanos.Valor!.Datos);

            insert.LanzarErrorSiFalla();

            var ciudadanosImagenes = await _ciudadanosReportadosEndpoint.ObtenerImagenes();

            ciudadanosImagenes.LanzarErrorSiFalla();

            var insertImagenes = await _ciudadanosRepository.ActualizarImagenes(ciudadanosImagenes.Valor!);

            insertImagenes.LanzarErrorSiFalla();
        }
    }
}
