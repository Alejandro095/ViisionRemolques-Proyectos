using Microsoft.AspNetCore.Mvc;
using ViisionRemolques.Entities;
using ViisionRemolques.Repositories;

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
