using Microsoft.AspNetCore.Mvc;
using ViisionRemolques.Repositories.Eventos;

namespace ViisionRemolques.Controllers
{
    [ApiController]
    [Route("/eventos")]
    public class EventosController : ControllerBase
    {
        private readonly EventoANPRRepository _eventoANPRRepository;
        private readonly EventoCapturaFacialRepository _eventoCapturaFacialRepository;
        private readonly EventoTraficoRodadoRepository _eventoTraficoRodadoRepository;
        private readonly EventoAlarmaRecuentoPersonasRepository _eventoAlarmaRecuentoPersonasRepository;
        private readonly EventoArmadoPistaPersonaRepository _eventoArmadoPistaPersonaRepository;
        private readonly EventoDeteccionTipoMultiobjetivoRepository _eventoDeteccionTipoMultiobjetivoRepository;
        private readonly EventoSmartRepository _eventoSmartRepository;
        private readonly EventoRecuentoPersonasRepository _eventoRecuentoPersonasRepository;

        public EventosController(
            EventoANPRRepository eventoANPRRepository,
            EventoCapturaFacialRepository eventoCapturaFacialRepository,
            EventoTraficoRodadoRepository eventoTraficoRodadoRepository,
            EventoAlarmaRecuentoPersonasRepository eventoAlarmaRecuentoPersonasRepository,
            EventoArmadoPistaPersonaRepository eventoArmadoPistaPersonaRepository,
            EventoDeteccionTipoMultiobjetivoRepository eventoDeteccionTipoMultiobjetivoRepository,
            EventoSmartRepository eventoSmartRepository,
            EventoRecuentoPersonasRepository eventoRecuentoPersonasRepository)
        {
            _eventoANPRRepository = eventoANPRRepository;
            _eventoCapturaFacialRepository = eventoCapturaFacialRepository;
            _eventoTraficoRodadoRepository = eventoTraficoRodadoRepository;
            _eventoAlarmaRecuentoPersonasRepository = eventoAlarmaRecuentoPersonasRepository;
            _eventoArmadoPistaPersonaRepository = eventoArmadoPistaPersonaRepository;
            _eventoDeteccionTipoMultiobjetivoRepository = eventoDeteccionTipoMultiobjetivoRepository;
            _eventoSmartRepository = eventoSmartRepository;
            _eventoRecuentoPersonasRepository = eventoRecuentoPersonasRepository;
        }

        [HttpGet("anpr/pendientes")]
        public async Task<IActionResult> ObtenerPendientesANPR()
        {
            return Ok(await _eventoANPRRepository.ObtenerPendientesAsync());
        }

        [HttpGet("captura-facial/pendientes")]
        public async Task<IActionResult> ObtenerPendientesCapturaFacial()
        {
            return Ok(await _eventoCapturaFacialRepository.ObtenerPendientesAsync());
        }

        [HttpGet("trafico-rodado/pendientes")]
        public async Task<IActionResult> ObtenerPendientesTraficoRodado()
        {
            return Ok(await _eventoTraficoRodadoRepository.ObtenerPendientesAsync());
        }

        [HttpGet("alarma-recuento-personas/pendientes")]
        public async Task<IActionResult> ObtenerPendientesAlarmaRecuentoPersonas()
        {
            return Ok(await _eventoAlarmaRecuentoPersonasRepository.ObtenerPendientesAsync());
        }

        [HttpGet("armado-pista-persona/pendientes")]
        public async Task<IActionResult> ObtenerPendientesArmadoPistaPersona()
        {
            return Ok(await _eventoArmadoPistaPersonaRepository.ObtenerPendientesAsync());
        }

        [HttpGet("deteccion-tipo-multiobjetivo/pendientes")]
        public async Task<IActionResult> ObtenerPendientesDeteccionTipoMultiobjetivo()
        {
            return Ok(await _eventoDeteccionTipoMultiobjetivoRepository.ObtenerPendientesAsync());
        }

        [HttpGet("smart/pendientes")]
        public async Task<IActionResult> ObtenerPendientesSmart()
        {
            return Ok(await _eventoSmartRepository.ObtenerPendientesAsync());
        }

        [HttpGet("recuento-personas/pendientes")]
        public async Task<IActionResult> ObtenerPendientesRecuentoPersonas()
        {
            return Ok(await _eventoRecuentoPersonasRepository.ObtenerPendientesAsync());
        }
    }
}
