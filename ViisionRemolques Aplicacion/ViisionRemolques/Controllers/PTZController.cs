using Microsoft.AspNetCore.Mvc;
using ViisionRemolques.Repositories;
using ViisionRemolques.Services.ISAPI.PTZ;
using System.ComponentModel.DataAnnotations;

namespace ViisionRemolques.Controllers
{
    [ApiController]
    [Route("camaras/{idInterno:long}/ptz")]
    public class PTZController : Controller
    {
        private readonly PtzService _ptzService;
        private readonly CamaraRepository _camaraRepository;

        public PTZController(PtzService ptzService, CamaraRepository camaraRepository)
        {
            _ptzService = ptzService;
            _camaraRepository = camaraRepository;
        }

        [HttpPut]
        [Route("mover/{pan:int}/{tilt:int}")]
        public async Task<IActionResult> Mover(
            [Range(1, long.MaxValue, ErrorMessage = "El parámetro '{0}' debe ser un valor positivo.")]
            long idInterno,
            [Range(-100, 100, ErrorMessage = "El valor de {0} debe estar entre -100 y 100.")]
            int pan,
            [Range(-100, 100, ErrorMessage = "El valor de {0} debe estar entre -100 y 100.")]
            int tilt,
            CancellationToken ct)
        {
            var camara = await _camaraRepository.ObtenerPorIdInternoAsync(idInterno);
            if (camara is null)
                return Problem(
                    detail: $"No se encontró ninguna cámara con el id '{idInterno}'.",
                    statusCode: StatusCodes.Status404NotFound,
                    title: "Recurso no encontrado"
                );

            var resultado = await _ptzService.MoverAsync(camara, pan, tilt, ct);
            if (!resultado.Exito)
                return Problem(
                    title: "Error",
                    detail: resultado.Error,
                    statusCode: StatusCodes.Status400BadRequest
                );

            return Ok();
        }

        [HttpPut]
        [Route("zoom/{valor:int}")]
        public async Task<IActionResult> Zoom(
            [Range(1, long.MaxValue, ErrorMessage = "El parámetro '{0}' debe ser un valor positivo.")]
            long idInterno,
            [Range(-100, 100, ErrorMessage = "El valor de {0} debe estar entre -100 y 100.")]
            int valor, 
            CancellationToken ct)
        {
            var camara = await _camaraRepository.ObtenerPorIdInternoAsync(idInterno);
            if (camara is null)
                return Problem(
                    detail: $"No se encontró ninguna cámara con el id '{idInterno}'.",
                    statusCode: StatusCodes.Status404NotFound,
                    title: "Recurso no encontrado"
                );

            var resultado = await _ptzService.ZoomAsync(camara, valor, ct);
            if (!resultado.Exito)
                return Problem(
                    title: "Error",
                    detail: resultado.Error,
                    statusCode: StatusCodes.Status400BadRequest
                );

            return Ok();
        }

        [HttpPut]
        [Route("enfoque/{valor:int}")]
        public async Task<IActionResult> Enfocar(
            [Range(1, long.MaxValue, ErrorMessage = "El parámetro '{0}' debe ser un valor positivo.")]
            long idInterno,
            [Range(-100, 100, ErrorMessage = "El valor de {0} debe estar entre -100 y 100.")]
            int valor,
            CancellationToken ct)
        {
            var camara = await _camaraRepository.ObtenerPorIdInternoAsync(idInterno);
            if (camara is null)
                return Problem(
                    detail: $"No se encontró ninguna cámara con el id '{idInterno}'.",
                    statusCode: StatusCodes.Status404NotFound,
                    title: "Recurso no encontrado"
                );

            var resultado = await _ptzService.EnfocarAsync(camara, valor, ct);
            if (!resultado.Exito)
                return Problem(
                    title: "Error",
                    detail: resultado.Error,
                    statusCode: StatusCodes.Status400BadRequest
                );

            return Ok();
        }

        [HttpPut]
        [Route("iris/{valor:int}")]
        public async Task<IActionResult> AjustarIris(
            [Range(1, long.MaxValue, ErrorMessage = "El parámetro '{0}' debe ser un valor positivo.")]
            long idInterno,
            [Range(-100, 100, ErrorMessage = "El valor de {0} debe estar entre -100 y 100.")]
            int valor,
            CancellationToken ct)
        {
            var camara = await _camaraRepository.ObtenerPorIdInternoAsync(idInterno);
            if (camara is null)
                return Problem(
                    detail: $"No se encontró ninguna cámara con el id '{idInterno}'.",
                    statusCode: StatusCodes.Status404NotFound,
                    title: "Recurso no encontrado"
                );

            var resultado = await _ptzService.AjustarIrisAsync(camara, valor, ct);
            if (!resultado.Exito)
                return Problem(
                    title: "Error",
                    detail: resultado.Error,
                    statusCode: StatusCodes.Status400BadRequest
                );

            return Ok();
        }

        [HttpPut]
        [Route("escobilla")]
        public async Task<IActionResult> Escobilla(
            [Range(1, long.MaxValue, ErrorMessage = "El parámetro '{0}' debe ser un valor positivo.")]
            long idInterno,
            CancellationToken ct)
        {
            var camara = await _camaraRepository.ObtenerPorIdInternoAsync(idInterno);
            if (camara is null)
                return Problem(
                    detail: $"No se encontró ninguna cámara con el id '{idInterno}'.",
                    statusCode: StatusCodes.Status404NotFound,
                    title: "Recurso no encontrado"
                );

            var resultado = await _ptzService.EscobillaAsync(camara, ct);
            if (!resultado.Exito)
                return Problem(
                    title: "Error",
                    detail: resultado.Error,
                    statusCode: StatusCodes.Status400BadRequest
                );

            return Ok();
        }

        [HttpPut]
        [Route("enfoque-auxiliar")]
        public async Task<IActionResult> EnfoqueAuxiliar(
            [Range(1, long.MaxValue, ErrorMessage = "El parámetro '{0}' debe ser un valor positivo.")]
            long idInterno,
            CancellationToken ct)
        {
            var camara = await _camaraRepository.ObtenerPorIdInternoAsync(idInterno);
            if (camara is null)
                return Problem(
                    detail: $"No se encontró ninguna cámara con el id '{idInterno}'.",
                    statusCode: StatusCodes.Status404NotFound,
                    title: "Recurso no encontrado"
                );

            var resultado = await _ptzService.EnfoqueAuxiliarAsync(camara, ct);
            if (!resultado.Exito)
                return Problem(
                    title: "Error",
                    detail: resultado.Error,
                    statusCode: StatusCodes.Status400BadRequest
                );

            return Ok();
        }

        [HttpPut]
        [Route("inicializacion-objetivo")]
        public async Task<IActionResult> InicializarObjetivo(
            [Range(1, long.MaxValue, ErrorMessage = "El parámetro '{0}' debe ser un valor positivo.")]
            long idInterno,
            CancellationToken ct)
        {
            var camara = await _camaraRepository.ObtenerPorIdInternoAsync(idInterno);
            if (camara is null)
                return Problem(
                    detail: $"No se encontró ninguna cámara con el id '{idInterno}'.",
                    statusCode: StatusCodes.Status404NotFound,
                    title: "Recurso no encontrado"
                );

            var resultado = await _ptzService.InicializarObjetivoAsync(camara, ct);
            if (!resultado.Exito)
                return Problem(
                    title: "Error",
                    detail: resultado.Error,
                    statusCode: StatusCodes.Status400BadRequest
                );

            return Ok();
        }

        [HttpPut]
        [Route("calibracion-zoom")]
        public async Task<IActionResult> CalibrarZoom(
            [Range(1, long.MaxValue, ErrorMessage = "El parámetro '{0}' debe ser un valor positivo.")]
            long idInterno,
            CancellationToken ct)
        {
            var camara = await _camaraRepository.ObtenerPorIdInternoAsync(idInterno);
            if (camara is null)
                return Problem(
                    detail: $"No se encontró ninguna cámara con el id '{idInterno}'.",
                    statusCode: StatusCodes.Status404NotFound,
                    title: "Recurso no encontrado"
                );

            var resultado = await _ptzService.CalibrarZoomAsync(camara, ct);
            if (!resultado.Exito)
                return Problem(
                    title: "Error",
                    detail: resultado.Error,
                    statusCode: StatusCodes.Status400BadRequest
                );

            return Ok();
        }
    }

}
