using Microsoft.AspNetCore.Mvc;
using ViisionRemolques.Repositories;
using ViisionRemolques.Services.ISAPI;
using System.ComponentModel.DataAnnotations;

namespace ViisionRemolques.Controllers
{
    [ApiController]
    [Route("fdlib")]
    public class FDLibController : Controller
    {
        private const long IdInternoCamara = 1;
        private readonly FDLibService _fdLibService;
        private readonly CamaraRepository _camaraRepository;

        public FDLibController(FDLibService fdLibService, CamaraRepository camaraRepository)
        {
            _fdLibService = fdLibService;
            _camaraRepository = camaraRepository;
        }

        [HttpGet]
        [Route("fdid")]
        public async Task<IActionResult> ObtenerFdid(CancellationToken ct)
        {
            var camara = await _camaraRepository.ObtenerPorIdInternoAsync(IdInternoCamara);
            if (camara is null)
                return Problem(
                    detail: $"No se encontró ninguna cámara con el id '{IdInternoCamara}'.",
                    statusCode: StatusCodes.Status404NotFound,
                    title: "Recurso no encontrado"
                );

            var resultado = await _fdLibService.IntentarObtenerFDIDLibreriaAsync(camara, ct);
            if (!resultado.Exito)
                return Problem(title: "Error", detail: resultado.Error, statusCode: StatusCodes.Status400BadRequest);

            return Ok(new { fdid = resultado.Valor });
        }

        [HttpPost]
        [Route("crear")]
        public async Task<IActionResult> CrearLibreria(CancellationToken ct)
        {
            var camara = await _camaraRepository.ObtenerPorIdInternoAsync(IdInternoCamara);
            if (camara is null)
                return Problem(
                    detail: $"No se encontró ninguna cámara con el id '{IdInternoCamara}'.",
                    statusCode: StatusCodes.Status404NotFound,
                    title: "Recurso no encontrado"
                );

            var resultado = await _fdLibService.CrearLibreriaAsync(camara, ct);
            if (!resultado.Exito)
                return Problem(title: "Error", detail: resultado.Error, statusCode: StatusCodes.Status400BadRequest);

            return Ok(new { fdid = resultado.Valor });
        }

        [HttpPost]
        [Route("{fdid}/benchmark")]
        public async Task<IActionResult> BenchmarkInsertarImagen(
    string fdid,
    [Required] string nombre,
    [Required] IFormFile imagen,
    [Required] int cantidad,
    int gradoParalelismo = 1,
    CancellationToken ct = default)
        {
            var camara = await _camaraRepository.ObtenerPorIdInternoAsync(IdInternoCamara);
            if (camara is null)
                return Problem(
                    detail: $"No se encontró ninguna cámara con el id '{IdInternoCamara}'.",
                    statusCode: StatusCodes.Status404NotFound,
                    title: "Recurso no encontrado"
                );

            using var ms = new MemoryStream();
            await imagen.CopyToAsync(ms, ct);
            var imagenBytes = ms.ToArray();

            var ok = 0;
            var fail = 0;
            var errores = new System.Collections.Concurrent.ConcurrentBag<string>();
            var semaforo = new SemaphoreSlim(gradoParalelismo);

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            var tareas = Enumerable.Range(1, cantidad).Select(async i =>
            {
                await semaforo.WaitAsync(ct);
                try
                {
                    var request = new FDLibService.InsertarImagenRequest(fdid, i.ToString(), $"{nombre}{i}", imagenBytes);
                    var resultado = await _fdLibService.InsertarImagenAsync(camara, request, ct);

                    if (resultado.Exito) Interlocked.Increment(ref ok);
                    else { Interlocked.Increment(ref fail); errores.Add($"id={i}: {resultado.Error}"); }
                }
                finally
                {
                    semaforo.Release();
                }
            });

            await Task.WhenAll(tareas);

            stopwatch.Stop();

            return Ok(new
            {
                ok,
                fail,
                gradoParalelismo,
                tiempoTotalMs = stopwatch.ElapsedMilliseconds,
                promedioMsPorRequest = stopwatch.ElapsedMilliseconds / cantidad,
                errores = errores.Take(10)
            });
        }

        [HttpPost]
        [Route("{fdid}/modelar")]
        public async Task<IActionResult> ModelarPendientes(string fdid, CancellationToken ct)
        {
            var camara = await _camaraRepository.ObtenerPorIdInternoAsync(IdInternoCamara);
            if (camara is null)
                return Problem(
                    detail: $"No se encontró ninguna cámara con el id '{IdInternoCamara}'.",
                    statusCode: StatusCodes.Status404NotFound,
                    title: "Recurso no encontrado"
                );

            var resultado = await _fdLibService.ModelarPendientesAsync(camara, fdid, ct);
            if (!resultado.Exito)
                return Problem(title: "Error", detail: resultado.Error, statusCode: StatusCodes.Status400BadRequest);

            return Ok();
        }

        [HttpDelete]
        [Route("{fdid}/imagenes/{pid}")]
        public async Task<IActionResult> BorrarImagen(string fdid, string pid, CancellationToken ct)
        {
            var camara = await _camaraRepository.ObtenerPorIdInternoAsync(IdInternoCamara);
            if (camara is null)
                return Problem(
                    detail: $"No se encontró ninguna cámara con el id '{IdInternoCamara}'.",
                    statusCode: StatusCodes.Status404NotFound,
                    title: "Recurso no encontrado"
                );

            var resultado = await _fdLibService.BorrarImagenAsync(camara, fdid, pid, ct);
            if (!resultado.Exito)
                return Problem(title: "Error", detail: resultado.Error, statusCode: StatusCodes.Status400BadRequest);

            return Ok();
        }

        [HttpGet]
        [Route("{fdid}/registros")]
        public async Task<IActionResult> ConsultarRegistros(string fdid, CancellationToken ct)
        {
            var camara = await _camaraRepository.ObtenerPorIdInternoAsync(IdInternoCamara);
            if (camara is null)
                return Problem(
                    detail: $"No se encontró ninguna cámara con el id '{IdInternoCamara}'.",
                    statusCode: StatusCodes.Status404NotFound,
                    title: "Recurso no encontrado"
                );

            var resultado = await _fdLibService.ConsultarRegistrosAsync(camara, fdid, ct);
            if (!resultado.Exito)
                return Problem(title: "Error", detail: resultado.Error, statusCode: StatusCodes.Status400BadRequest);

            return Ok(resultado.Valor);
        }
    }
}