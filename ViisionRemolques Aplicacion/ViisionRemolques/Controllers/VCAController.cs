using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using ViisionRemolques.Enums;
using ViisionRemolques.Repositories;
using ViisionRemolques.Services.ISAPI;
using ViisionRemolques.Services.ISAPI.VCA;

namespace ViisionRemolques.Controllers
{
    [ApiController]
    [Route("camaras/{idInterno:long}/vca")]
    public class VCAController : ControllerBase
    {
        private readonly CamaraRepository _camaraRepository;
        private readonly VcaService _vcaService;
        public VCAController(CamaraRepository camaraRepository, VcaService vcaService)
        {
            _camaraRepository = camaraRepository;
            _vcaService = vcaService;
        }

        [HttpGet]
        [Route("modo")]
        public async Task<IActionResult> ObtenerModoActual(
            [Range(1, long.MaxValue, ErrorMessage = "El parámetro '{0}' debe ser un valor positivo.")]
            long idInterno,
            CancellationToken ct)
        {
            var camara = await _camaraRepository.ObtenerPorIdInternoAsync(idInterno);

            if (camara is null) return Problem(
                detail: $"No se encontró ninguna cámara con el id '{idInterno}'.",
                statusCode: StatusCodes.Status404NotFound,
                title: "Recurso no encontrado"
            );

            var resultado = await _vcaService.ObtenerModoActualAsync(camara, ct);

            if (!resultado.Exito)
                return Problem(
                    title: "Error",
                    detail: resultado.Error,
                    statusCode: StatusCodes.Status400BadRequest
                );

            return Ok(new
            {
                vca = resultado.Valor.Info().Titulo
            });
        }



        [HttpPut]
        [Route("modo/{vca}")]
        public async Task<IActionResult> CambiarModo(
            [Range(1, long.MaxValue, ErrorMessage = "El parámetro '{0}' debe ser un valor positivo.")]
            long idInterno,
            [Required(ErrorMessage = "El parámetro '{0}' es obligatorio.")]
            [StringLength(100, ErrorMessage = "El parámetro '{0}' no puede superar los {1} caracteres.")]
            string vca,
            CancellationToken ct)
        {
            var camara = await _camaraRepository.ObtenerPorIdInternoAsync(idInterno);

            if (camara is null) return Problem(
                detail: $"No se encontró ninguna cámara con el id '{idInterno}'.",
                statusCode: StatusCodes.Status404NotFound,
                title: "Recurso no encontrado"
            );


            if (!VCAModoCatalogo.TryParseTitulo(vca, out var modo))
            {
                return Problem(
                    detail: $"'{vca}' no es un modo VCA válido.",
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Parámetro inválido"
                );
            }                

            var resultado = await _vcaService.CambiarModoAsync(camara, modo, ct);

            if (!resultado.Exito)
                return Problem(
                    title: "Error",
                    detail: resultado.Error,
                    statusCode: StatusCodes.Status400BadRequest
                );

            return Ok(new
            {
                mensaje = $"El modo VCA ha sido actualizado correctamente a '{vca}'. El dispositivo se encuentra en proceso de reinicio y reanudará su operación en unos momentos.",
                vca = vca,
            });
        }

        private record ModosVCA(string Id, string Titulo);

        [HttpGet]
        [Route("modos")]
        public async Task<IActionResult> ObtenerModosSoportador(
            [Range(1, long.MaxValue, ErrorMessage = "El parámetro '{0}' debe ser un valor positivo.")]
            long idInterno,
            CancellationToken ct)
        {
            var camara = await _camaraRepository.ObtenerPorIdInternoAsync(idInterno);

            if (camara is null) return Problem(
                detail: $"No se encontró ninguna cámara con el id '{idInterno}'.",
                statusCode: StatusCodes.Status404NotFound,
                title: "Recurso no encontrado"
            );

            var resultado = await _vcaService.ObtenerModosSoportadosAsync(camara, ct);

            if (!resultado.Exito)
                return Problem(
                    title: "Error",
                    detail: resultado.Error,
                    statusCode: StatusCodes.Status400BadRequest
                );

            return Ok(resultado.Valor!.Select(modo => 
                new ModosVCA(modo.Info().Titulo, modo.Info().Descripcion)));
        }
    }
}
