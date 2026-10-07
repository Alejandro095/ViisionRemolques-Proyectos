using Microsoft.AspNetCore.Mvc;
using ViisionRemolques.Repositories;
using ViisionRemolques.Services.ISAPI;

namespace ViisionRemolques.Controllers
{
    [ApiController]
    [Route("test/analiticas")]
    public class TestController : Controller
    {
        private readonly HeatmapService _heatmapService;
        private readonly InterseccionesService _interseccionesService;
        private readonly CamaraRepository _camaraRepository;

        public TestController(
            HeatmapService heatmapService,
            InterseccionesService interseccionesService,
            CamaraRepository camaraRepository)
        {
            _heatmapService = heatmapService;
            _interseccionesService = interseccionesService;
            _camaraRepository = camaraRepository;
        }

        [HttpPost("heatmap/{tipoReporte}/{modeloEstadistico}")]
        public async Task<IActionResult> TestHeatmap(CancellationToken ct, string tipoReporte = "daily", string modeloEstadistico = "duration")
        {
            long idInterno = 3;
            var request = new HeatmapService.HeatmapInformacionRequest
            {
                Fecha = DateTime.Now,
                TipoReporte = tipoReporte,
                ModeloEstadistico = modeloEstadistico
            };

            var camara = await _camaraRepository.ObtenerPorIdInternoAsync(idInterno);
            if (camara is null)
            {
                return Problem(
                    detail: $"No se encontró ninguna cámara con el id '{idInterno}'.",
                    statusCode: StatusCodes.Status404NotFound,
                    title: "Recurso no encontrado"
                );
            }

            var resultado = await _heatmapService.ObtenerHeatmap(camara, request, ct);
            if (!resultado.Exito)
            {
                return Problem(
                    title: "Error",
                    detail: resultado.Error,
                    statusCode: StatusCodes.Status400BadRequest
                );
            }

            return Ok(resultado.Valor);
        }

        [HttpPost("intersecciones/{tipoReporte}/{entrada}")]
        public async Task<IActionResult> TestIntersecciones(CancellationToken ct, string entrada = "A", string tipoReporte = "daily")
        {
            long idInterno = 3;
            var request = new InterseccionesService.InterseccionInformacionRequest
            {
                Fecha = DateTime.Now,
                TipoReporte = tipoReporte,
                Entrada = entrada
            };

            var camara = await _camaraRepository.ObtenerPorIdInternoAsync(idInterno);
            if (camara is null)
            {
                return Problem(
                    detail: $"No se encontró ninguna cámara con el id '{idInterno}'.",
                    statusCode: StatusCodes.Status404NotFound,
                    title: "Recurso no encontrado"
                );
            }

            var resultado = await _interseccionesService.ObtenerInterseccionAsync(camara, request, ct);
            if (!resultado.Exito)
            {
                return Problem(
                    title: "Error",
                    detail: resultado.Error,
                    statusCode: StatusCodes.Status400BadRequest
                );
            }

            return Ok(resultado.Valor);
        }
    }
}