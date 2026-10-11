using Hangfire;
using Microsoft.AspNetCore.Mvc;
using ViisionRemolques.Enums;
using ViisionRemolques.Jobs;
using ViisionRemolques.Parsing;
using ViisionRemolques.Parsing.Models;
using ViisionRemolques.Repositories.Eventos;
using ViisionRemolques.Services;

namespace ViisionRemolques.Controllers
{
    [ApiController]
    [Route("webhook")]
    public class WebhookController : ControllerBase
    {
        private readonly ILogger<WebhookController> _logger;
        private readonly AlmacenamientoService _almacenamientoService;
        private readonly WebhookPayloadExtractorService _webhookPayloadExtractorService;
        private readonly EventoSmartRepository _eventoSmartRepository;

        public WebhookController(
            ILogger<WebhookController> logger,
            AlmacenamientoService almacenamientoService,
            WebhookPayloadExtractorService webhookPayloadExtractorService,
            EventoSmartRepository eventoSmartRepository)
        {
            _logger = logger;
            _almacenamientoService = almacenamientoService;
            _webhookPayloadExtractorService = webhookPayloadExtractorService;
            _eventoSmartRepository = eventoSmartRepository;
        }

        [HttpPost]
        [DisableRequestSizeLimit]
        public async Task<IActionResult> Post([FromServices] IBackgroundJobClient Jobs, CancellationToken cancellationToken)
        {
            try
            {
                var webhookPayload = await _webhookPayloadExtractorService.Extraer(Request);

                if (webhookPayload.Body is null) return BadRequest();

                EventoExtractorModelo? evento = CameraEventParser.Parse(webhookPayload.Body);

                if (evento is not null && (
                    evento.Evento.EventType == "heartBeat" ||
                    evento.Evento.EventType == "duration"))
                {
                    return Ok();
                }

                if (evento is not null && evento.Evento.VCAModo == VCAModoEnum.Ninguno) return Ok();

                var imagenesPaths = await _almacenamientoService.GuardarImagen(webhookPayload.Imagenes);

                // Encola el trabajo y libera inmediatamente la petición del webhook
                Jobs.Enqueue<EventosWebhookJob>(
                    job => job.ProcesarEventoAsync(evento, imagenesPaths, webhookPayload.Body, cancellationToken)
                );

                return Ok();
            }
            catch (Exception exception)
            {
                _logger.LogCritical(exception.ToString());
                return Ok();
            }
        }
    }
}