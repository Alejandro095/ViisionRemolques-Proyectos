using Microsoft.AspNetCore.Mvc;
using ViisionRemolques.Enums;
using ViisionRemolques.Repositories;
using ViisionRemolques.Services.ISAPI;

namespace ViisionRemolques.Controllers
{
    [ApiController]
    [Route("camaras/{idInterno:long}/vca")]
    public class VCAController : ControllerBase
    {
        private readonly CamaraRepository _camaraRepository;
        private readonly VCAService _vcaService;
        public VCAController(CamaraRepository camaraRepository, VCAService VCAService)
        {
            _camaraRepository = camaraRepository;
            _vcaService = VCAService;
        }

        [HttpGet]
        [Route("modo")]
        public async Task<IActionResult> ObtenerModoActual(long idInterno, CancellationToken ct)
        {
            var camara = await _camaraRepository.ObtenerPorIdInternoAsync(idInterno);

            if (camara is null) return Problem(
                detail: $"No se encontró ninguna cámara con el id '{idInterno}'.",
                statusCode: StatusCodes.Status404NotFound,
                title: "Recurso no encontrado"
            );

            if (camara.Modelo == ModeloCamaraEnum.HikvisionRadar.ObtenerCodigoModelo())
            {
                return Problem(
                    title: "Operación no soportada",
                    detail: $"El modelo de cámara '{camara.Modelo}' no es compatible con esta operación.",
                    statusCode: StatusCodes.Status400BadRequest
                );
            }

            string vcaActual = await _vcaService.ObtenerModoActualAsync(camara, ct);

            return Ok(new
            {
                vca = vcaActual
            });
        }



        [HttpPut]
        [Route("modo")]
        public async Task<IActionResult> CambiarModo(
            long idInterno,
            [FromQuery] string vca,
            CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(vca))
            {
                return Problem(
                    title: "Petición inválida",
                    detail: "El parámetro de consulta 'vca' es requerido y no puede estar vacío.",
                    statusCode: StatusCodes.Status400BadRequest
                );
            }

            var camara = await _camaraRepository.ObtenerPorIdInternoAsync(idInterno);

            if (camara is null) return Problem(
                detail: $"No se encontró ninguna cámara con el id '{idInterno}'.",
                statusCode: StatusCodes.Status404NotFound,
                title: "Recurso no encontrado"
            );

            if (camara.Modelo == ModeloCamaraEnum.HikvisionRadar.ObtenerCodigoModelo())
            {
                return Problem(
                    title: "Operación no soportada",
                    detail: $"El modelo de cámara '{camara.Modelo}' no es compatible con esta operación.",
                    statusCode: StatusCodes.Status400BadRequest
                );
            }

            // Cambiar modo VCA y reiniciar la cámara
            await _vcaService.CambiarModoAsync(camara, vca, reiniciarAlFinalizar: true, ct);

            return Ok(new
            {
                mensaje = $"Modo VCA actualizado exitosamente a '{vca}'. La cámara se está reiniciando y estará disponible en unos momentos.",
                vcaNuevo = vca,
                reiniciando = true
            });
        }


    }
}
