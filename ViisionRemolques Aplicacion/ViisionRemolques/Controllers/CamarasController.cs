using Microsoft.AspNetCore.Mvc;
using ViisionRemolques.Entities;
using ViisionRemolques.Repositories;
using ViisionRemolques.Services.ISAPI;
using static ViisionRemolques.Services.ISAPI.InterseccionesService;

namespace ViisionRemolques.Controllers
{
    [ApiController]
    [Route("/camaras")]
    public class CamarasController : ControllerBase
    {
        private readonly CamaraRepository _camaraRepository;
        private readonly InterseccionesService _interseccionesService;
        public CamarasController(CamaraRepository camaraRepository, InterseccionesService interseccionesService) {

            _camaraRepository = camaraRepository;
            _interseccionesService = interseccionesService;
        }

        [HttpGet]
        [Route("/test/id")]
        public async Task<IActionResult> Test(CancellationToken ct, long idInterno = 3) {

            // 1. Obtener la cámara desde el repositorio
            var camara = await _camaraRepository.ObtenerPorIdInternoAsync(idInterno);

            if (camara == null || string.IsNullOrWhiteSpace(camara.Go2Rtc))
            {
                return NotFound(new { message = $"No se encontró la cámara o el identificador go2rtc para el idInterno: {idInterno}" });
            }
           

            return Ok(await _interseccionesService.ObtenerInterseccionAsync(camara, new InterseccionInformacionRequest 
            {
                Entrada = "A",
                Fecha = DateTime.Now,
                TipoReporte = "daily"
            }, ct));
        }

        [HttpGet]
        public async Task<IActionResult> Listar()
        {
            var camaras = await _camaraRepository.ObtenerTodasAsync();

            return Ok(camaras.Select(c => new
            {
                c.IdInterno,
                c.Nombre,
                c.Modelo,
                c.Activo,
                caracteristicas = new 
                {
                    c.SoportaPtz,
                    c.SoportaAudioBidireccional,
                },
                alarmas = new 
                {
                    EventoSmart = new
                    {
                        DeteccionIntrusiones = c.EventoSmartDeteccionIntrusiones,
                        DeteccionCruceLinea = c.EventoSmartDeteccionCruceLinea,
                        DeteccionEntradaArea=c.EventoSmartDeteccionEntradaArea,
                        DeteccionSalidaArea=c.EventoSmartDeteccionSalidaArea,
                        EventoCombinado=c.EventoSmartEventoCombinado
                    },
                },
            }));
        }
    }
}
