using ViisionRemolques.Enums;
using ViisionRemolques.Parsing;
using ViisionRemolques.Parsing.Models;
using ViisionRemolques.Repositories.Eventos;
using ViisionRemolques.Services;

namespace ViisionRemolques.Jobs
{
    public class EventosWebhookJob
    {
        private readonly ILogger<EventosWebhookJob> _logger;
        private readonly AlmacenamientoImagenesService _almacenamientoImagenesService;


        private readonly EventoSmartRepository _eventoSmartRepository;
        private readonly EventoAlarmaRecuentoPersonasRepository _eventoAlarmaRecuentoPersonasRepository;
        private readonly EventoANPRRepository _eventoANPRRepository;
        private readonly EventoArmadoPistaPersonaRepository _eventoArmadoPersonaRepository;
        private readonly EventoCapturaFacialRepository _eventoCapturaFacialRepository;
        private readonly EventoDeteccionTipoMultiobjetivoRepository _eventoDeteccionTipoMultiobjetivoepository;
        private readonly EventoRecuentoPersonasRepository _eventoRecuentoPersonasRepository;
        private readonly EventoTraficoRodadoRepository _eventoTraficoRodadoRepository;

        public EventosWebhookJob(
            ILogger<EventosWebhookJob> logger,
            AlmacenamientoImagenesService almacenamientoImagenesService,
            EventoSmartRepository eventoSmartRepository,
            EventoAlarmaRecuentoPersonasRepository eventoAlarmaRecuentoPersonasRepository,
            EventoANPRRepository eventoANPRRepository,
            EventoArmadoPistaPersonaRepository eventoArmadoPersonaRepository,
            EventoCapturaFacialRepository eventoCapturaFacialRepository,
            EventoDeteccionTipoMultiobjetivoRepository eventoDeteccionTipoMultiobjetivoepository,
            EventoRecuentoPersonasRepository eventoRecuentoPersonasRepository,
            EventoTraficoRodadoRepository eventoTraficoRodadoRepository)
        {
            _logger = logger;
            _almacenamientoImagenesService = almacenamientoImagenesService;
            _eventoSmartRepository = eventoSmartRepository;
            _eventoAlarmaRecuentoPersonasRepository = eventoAlarmaRecuentoPersonasRepository;
            _eventoANPRRepository = eventoANPRRepository;
            _eventoArmadoPersonaRepository = eventoArmadoPersonaRepository;
            _eventoCapturaFacialRepository = eventoCapturaFacialRepository;
            _eventoDeteccionTipoMultiobjetivoepository = eventoDeteccionTipoMultiobjetivoepository;
            _eventoRecuentoPersonasRepository = eventoRecuentoPersonasRepository;
            _eventoTraficoRodadoRepository = eventoTraficoRodadoRepository;
        }

        public async Task ProcesarEventoAsync(
            EventoExtractorModelo? eventoExtractorModelo, 
            List<string> imagenesPaths, 
            string payload, 
            CancellationToken cancellationToken)
        {
            try
            {
                if (eventoExtractorModelo == null) return;

                // Insertar en la base de datos según el tipo de VCA
                switch (eventoExtractorModelo.Evento.VCAModo)
                {
                    case VCAModoEnum.AlarmaRecuentoPersonas:
                        await _eventoAlarmaRecuentoPersonasRepository
                            .InsertarAsync(eventoExtractorModelo, ImagenesPaths: imagenesPaths, Payload: payload);
                        break;
                    case VCAModoEnum.ANPR:
                        await _eventoANPRRepository
                            .InsertarAsync(eventoExtractorModelo, ImagenesPaths: imagenesPaths, Payload: payload);
                        break;
                    case VCAModoEnum.ArmadoPistaPersona:
                        await _eventoArmadoPersonaRepository
                            .InsertarAsync(eventoExtractorModelo, ImagenesPaths: imagenesPaths, Payload: payload);
                        break;
                    case VCAModoEnum.CapturaFacial:
                        await _eventoCapturaFacialRepository
                            .InsertarAsync(eventoExtractorModelo, ImagenesPaths: imagenesPaths, Payload: payload);
                        break;
                    case VCAModoEnum.DeteccionTipoMultiObjetivo:
                        await _eventoDeteccionTipoMultiobjetivoepository
                            .InsertarAsync(eventoExtractorModelo, ImagenesPaths: imagenesPaths, Payload: payload);
                        break;
                    case VCAModoEnum.EventoSmart:
                        await _eventoSmartRepository
                            .InsertarAsync(eventoExtractorModelo, ImagenesPaths: imagenesPaths, Payload: payload);
                        break;
                    case VCAModoEnum.RecuentoPersonas:
                        await _eventoRecuentoPersonasRepository
                            .InsertarAsync(eventoExtractorModelo, ImagenesPaths: imagenesPaths, Payload: payload);
                        break;
                    case VCAModoEnum.TraficoRodado:
                        await _eventoTraficoRodadoRepository
                            .InsertarAsync(eventoExtractorModelo, ImagenesPaths: imagenesPaths, Payload: payload);
                        break;
                }
            }
            catch (Exception exception)
            {
                _logger.LogCritical(exception, "Error al procesar el evento webhook en el Job de Hangfire.");
                throw; // Se re-lanza para que Hangfire sepa que falló y pueda reintentar si está configurado
            }
        }

    }
}
