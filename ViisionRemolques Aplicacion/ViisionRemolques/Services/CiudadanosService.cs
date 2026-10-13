using Microsoft.AspNetCore.Mvc.ApplicationModels;
using ViisionRemolques.Enums;
using ViisionRemolques.Repositories;
using ViisionRemolques.Services.Centralia;
using ViisionRemolques.Services.Centralia.Endpoints;
using ViisionRemolques.Utils;
using static ViisionRemolques.Repositories.CiudadanosRepository;

namespace ViisionRemolques.Services
{
    public class CiudadanosService
    {

        private readonly CiudadanosEndpoint _ciudadanosReportadosEndpoint;
        private readonly CiudadanosRepository _ciudadanosRepository;
        private readonly PathsService _pathsService;
        private readonly CentraliaApiClientService _centraliaApiClientService;
        private readonly CatalogosVersionesRepository _catalogosVersionesRepository;

        public CiudadanosService(
            CiudadanosEndpoint ciudadanosReportadosEndpoint, 
            CiudadanosRepository ciudadanosRepository, 
            PathsService pathsService,
            CentraliaApiClientService centraliaApiClientService,
            CatalogosVersionesRepository catalogosVersionesRepository)
        {
            _ciudadanosReportadosEndpoint = ciudadanosReportadosEndpoint;
            _ciudadanosRepository = ciudadanosRepository;
            _pathsService = pathsService;
            _centraliaApiClientService = centraliaApiClientService;
            _catalogosVersionesRepository = catalogosVersionesRepository;
        }

        public async Task<Resultado> SincronizarCiudadanosReportados()
        {
            var versionCatalogo = await _catalogosVersionesRepository.ObtenerAsync(CatalogoVersionEnum.Ciudadanos);

            string? version = null;

            if (versionCatalogo.Exito && !string.IsNullOrWhiteSpace(versionCatalogo.Valor!.Version)) version = versionCatalogo.Valor!.Version;

            var ciudadanos = await _ciudadanosReportadosEndpoint.Obtener(version: version);

            if (!ciudadanos.Exito) return Resultado.Fallo(ciudadanos.Error!);

            if (ciudadanos.Valor is null || ciudadanos.Valor.Datos is null)
                return Resultado.Fallo("El payload del endpoint de ciudadanos blacklist viene incorrecto.");

            if (ciudadanos.Valor.Novedades == true)
            {
                var sqlRresultado = await _ciudadanosRepository.Actualizar(ciudadanos.Valor.Datos);

                if (!sqlRresultado.Exito) return Resultado.Fallo(sqlRresultado.Error!);

                await _catalogosVersionesRepository.ActualizarVersionAsync(CatalogoVersionEnum.Ciudadanos, ciudadanos.Valor.FechaGlobal);
            }

            return Resultado.Ok();
        }

        public async Task<Resultado> SincronizarCiudadanosReportadosImagenes()
        {
            var versionCatalogo = await _catalogosVersionesRepository.ObtenerAsync(CatalogoVersionEnum.CiudadanosRostros);

            string? version = null;

            if (versionCatalogo.Exito && !string.IsNullOrWhiteSpace(versionCatalogo.Valor!.Version)) version = versionCatalogo.Valor!.Version;

            var ciudadanos = await _ciudadanosReportadosEndpoint.ObtenerImagenes(version: version);

            if (!ciudadanos.Exito) return Resultado.Fallo(ciudadanos.Error!);

            if (ciudadanos.Valor is null)
                return Resultado.Fallo("El payload del endpoint de ciudadanos imagenes blacklist viene incorrecto.");

            if (ciudadanos.Valor?.Novedades == true)
            {
                var insert = await _ciudadanosRepository.ActualizarImagenes(ciudadanos.Valor?.Datos);

                if (!insert.Exito) return Resultado.Fallo(insert.Error!);

                await _catalogosVersionesRepository.ActualizarVersionAsync(CatalogoVersionEnum.CiudadanosRostros, ciudadanos.Valor?.FechaGlobal);
            }

            return Resultado.Ok();
        }


        public async Task<Resultado> ProcesarDiferenciasAsync()
        {
            var accionesResultado = await _ciudadanosRepository.ObtenerDiferencias();
            if (!accionesResultado.Exito) return Resultado.Fallo(accionesResultado.Error!);

            var carpetaFinal = _pathsService.GetRostrosPath();
            var carpetaTemp = _pathsService.GetRostrosTempPath();
            // Borrar carperta de imagenes temporales
            if (Directory.Exists(carpetaTemp)) Directory.Delete(carpetaTemp, recursive: true);
            Directory.CreateDirectory(carpetaTemp);


            // Imagenas antiguas se borran
            var elementosBorrar = accionesResultado.Valor!
                                     .Where(el => el.Accion == "Borrar").ToList();

            foreach (var lote in elementosBorrar.Chunk(100))
            {
                foreach (var elemento in lote)
                {
                    try
                    {
                        if (!string.IsNullOrEmpty(elemento.RutaArchivoLocal) && File.Exists(elemento.RutaArchivoLocal))
                            File.Delete(elemento.RutaArchivoLocal);
                    }
                    catch (IOException) {}
                }

                var idsLotes = lote.Select(f => f.ImagenIdExterno);

                var borrarResultado = await _ciudadanosRepository.BorrarEstadoFotosAsync(idsLotes);

                if (!borrarResultado.Exito) return Resultado.Fallo(borrarResultado.Error!);
            }


            // Nuevas imagenes se descargan

            var elementosDescagar = accionesResultado.Valor!
                                        .Where(el => el.Accion == "Descargar").ToList();

            foreach (var lote in elementosDescagar.Chunk(100))
            {
                var exitosos = new List<CiudadanoFotoEstadoEntity>();

                foreach (var fila in lote)
                {
                    if (fila.PathCentral is null || fila.HashCentral is null) continue;

                    var extension = Path.GetExtension(fila.PathCentral);

                    var rutaFinal = Path.Combine(carpetaFinal, $"{fila.ImagenIdExterno}{extension}");

                    var rutaTmp = Path.Combine(carpetaTemp, $"{fila.ImagenIdExterno}.tmp");

                    var descarga = await _centraliaApiClientService.DescargarArchivoAsync(fila.PathCentral, rutaTmp);

                    if (!descarga.Exito) continue;

                    Directory.CreateDirectory(Path.GetDirectoryName(rutaFinal)!);
                    File.Move(rutaTmp, rutaFinal, overwrite: true);

                    exitosos.Add(new CiudadanoFotoEstadoEntity(fila.ImagenIdExterno, fila.CiudadanoIdExterno, fila.HashCentral, rutaFinal, DateTime.UtcNow));
                }

                if (exitosos.Count > 0)
                {
                    var upsertResultado = await _ciudadanosRepository.ActualizarFotosEstadoAsync(exitosos);

                    if (!upsertResultado.Exito) return Resultado.Fallo(upsertResultado.Error!);
                }
            }

            return Resultado.Ok();
        }
    }
}
