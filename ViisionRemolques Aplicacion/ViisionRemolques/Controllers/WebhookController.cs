using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Net.Http.Headers;
using System.Net.Http.Headers;
using ViisionRemolques.Entities;
using ViisionRemolques.Enums;
using ViisionRemolques.Parsing;
using ViisionRemolques.Parsing.Extractors;
using ViisionRemolques.Parsing.Models;
using ViisionRemolques.Repositories;
using ViisionRemolques.Repositories.Eventos;
using ViisionRemolques.Services;

namespace ViisionRemolques.Controllers
{
    [ApiController]
    [Route("webhook")]
    public class WebhookController : ControllerBase
    {
        private readonly ILogger<WebhookController> _logger;
        private readonly AlmacenamientoImagenesService _almacenamientoImagenesService;
        private readonly WebhookPayloadExtractorService _webhookPayloadExtractorService;
        private readonly EventoSmartRepository _eventoSmartRepository;

        public WebhookController(
            ILogger<WebhookController> logger,
            AlmacenamientoImagenesService almacenamientoImagenesService,
            WebhookPayloadExtractorService webhookPayloadExtractorService,
            EventoSmartRepository eventoSmartRepository)
        {
            _logger = logger;
            _almacenamientoImagenesService = almacenamientoImagenesService;
            _webhookPayloadExtractorService = webhookPayloadExtractorService;
            _eventoSmartRepository = eventoSmartRepository;
        }

        [HttpPost]
        [DisableRequestSizeLimit]
        public async Task<IActionResult> Post()
        {
            try
            {
                var webhookPayload = await _webhookPayloadExtractorService.Extraer(Request);

                if (webhookPayload.Body is null)
                {
                    return BadRequest();
                }

                EventoExtractorModelo? evento = CameraEventParser.Parse(webhookPayload.Body);

                var imagenesPaths = await _almacenamientoImagenesService.Guardar(webhookPayload.Imagenes);

                if (evento is not null && (
                    evento.Evento.EventType == "heartBeat" || 
                    evento.Evento.EventType == "duration"))
                {
                    return Ok();
                }

                if (evento is null || evento.Evento.VCAModo == VCAModoEnum.Ninguno)
                {
                    return Ok();
                }

                switch (evento.Evento.VCAModo)
                {
                    case VCAModoEnum.EventoSmart:
                        await _eventoSmartRepository.InsertarAsync(evento, ImagenesPaths: imagenesPaths, Payload: webhookPayload.Body);
                        break;
                    case VCAModoEnum.RecuentoPersonas:
                        break;
                    case VCAModoEnum.CapturaFacial:
                        break;
                }

                return Ok();
            } catch (Exception exception)
            {
                _logger.LogCritical(exception.ToString());

                return Ok();
            }
        }
    }
}