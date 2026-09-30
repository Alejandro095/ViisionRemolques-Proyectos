using ViisionRemolques.Enums;
using ViisionRemolques.Parsing;
using ViisionRemolques.Parsing.Models;
using ViisionRemolques.Repositories.Eventos;
using ViisionRemolques.Services;

namespace ViisionRemolques.Jobs
{
    public class ProcesadorEventosWebhookJob
    {
        private readonly ILogger<ProcesadorEventosWebhookJob> _logger;
        private readonly AlmacenamientoImagenesService _almacenamientoImagenesService;


        private readonly EventoSmartRepository _eventoSmartRepository;
        private readonly EventoAlarmaRecuentoPersonasRepository _eventoAlarmaRecuentoPersonasRepository;
        private readonly EventoANPRRepository _eventoANPRRepository;
        private readonly EventoArmadoPistaPersonaRepository _eventoArmadoPersonaRepository;
        private readonly EventoCapturaFacialRepository _eventoCapturaFacialRepository;

        public ProcesadorEventosWebhookJob(
            ILogger<ProcesadorEventosWebhookJob> logger,
            AlmacenamientoImagenesService almacenamientoImagenesService,
            EventoSmartRepository eventoSmartRepository,
            EventoAlarmaRecuentoPersonasRepository eventoAlarmaRecuentoPersonasRepository,
            EventoANPRRepository eventoANPRRepository,
            EventoArmadoPistaPersonaRepository eventoArmadoPersonaRepository,
            EventoCapturaFacialRepository eventoCapturaFacialRepository)
        {
            _logger = logger;
            _almacenamientoImagenesService = almacenamientoImagenesService;
            _eventoSmartRepository = eventoSmartRepository;
            _eventoAlarmaRecuentoPersonasRepository = eventoAlarmaRecuentoPersonasRepository;
            _eventoANPRRepository = eventoANPRRepository;
            _eventoArmadoPersonaRepository = eventoArmadoPersonaRepository;
            _eventoCapturaFacialRepository = eventoCapturaFacialRepository;
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
                        // TODO: Implementar cuando esté disponible
                        break;
                    case VCAModoEnum.EventoSmart:
                        await _eventoSmartRepository
                            .InsertarAsync(eventoExtractorModelo, ImagenesPaths: imagenesPaths, Payload: payload);
                        break;
                    case VCAModoEnum.RecuentoPersonas:
                        // TODO: Implementar cuando esté disponible
                        break;
                    case VCAModoEnum.TraficoRodado:
                        // TODO: Implementar cuando esté disponible
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
