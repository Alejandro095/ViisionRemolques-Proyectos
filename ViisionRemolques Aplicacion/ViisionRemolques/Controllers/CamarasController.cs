using Microsoft.AspNetCore.Mvc;
using ViisionRemolques.Repositories;
using ViisionRemolques.Services.ISAPI;

namespace ViisionRemolques.Controllers
{
    [ApiController]
    [Route("/camaras")]
    public class CamarasController : ControllerBase
    {
        private readonly CamaraRepository _camaraRepository;
        public CamarasController(CamaraRepository camaraRepository) {

            _camaraRepository = camaraRepository;
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
                Funcionalidades = new 
                {
                    AudioBidireccional = c.Soporta_AudioBidireccional,
                    PTZ = new
                    {
                        Movimiento = c.Soporta_PtzMovimiento,
                        Zoom = c.Soporta_PtzZoom,
                        Enfoque = c.Soporta_PtzEnfoque,
                        Iris = c.Soporta_PtzIris,
                        Escobilla = c.Soporta_PtzEscobilla,
                        EnfoqueAuxiliar = c.Soporta_PtzEnfoqueAuxiliar,
                        InicializacionObjetivo = c.Soporta_PtzInicializacionObjetivo,
                        CalibracionZoom = c.Soporta_PtzCalibracionZoom
                    }
                }
            }));
        }
    }
}
