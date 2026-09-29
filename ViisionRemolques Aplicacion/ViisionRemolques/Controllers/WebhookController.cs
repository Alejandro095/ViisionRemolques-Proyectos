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
using ViisionRemolques.Services;

namespace ViisionRemolques.Controllers
{
    [ApiController]
    [Route("webhook")]
    public class WebhookController : ControllerBase
    {
        private readonly AlarmaDesconocidaLogRepository _alarmaDesconocidaLogRepository;
        private readonly ILogger<WebhookController> _logger;
        private readonly AlmacenamientoImagenesService _almacenamientoImagenesService;
        private readonly WebhookPayloadExtractorService _webhookPayloadExtractorService;

        public WebhookController(
            ILogger<WebhookController> logger,
            AlarmaDesconocidaLogRepository alarmasDesconocidasLogRepository,
            AlmacenamientoImagenesService almacenamientoImagenesService,
            WebhookPayloadExtractorService webhookPayloadExtractorService)
        {
            _logger = logger;
            _almacenamientoImagenesService = almacenamientoImagenesService;
            _webhookPayloadExtractorService = webhookPayloadExtractorService;
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
                    await _alarmaDesconocidaLogRepository.InsertarAsync(new AlarmaDesonocidaLogEntity()
                    {
                        IPCamara = HttpContext.Connection.RemoteIpAddress?.ToString(),
                        ContentType = Request.ContentType,
                        Evento = evento?.Evento.EventType ?? null,
                        Body = webhookPayload.Body,
                        Motivo = evento is null ? "PARSEO_ERROR" : "EXTRACTOR_DETALLES_ERROR"
                    });

                    return Ok();
                }

                switch (evento.Evento.VCAModo)
                {
                    case VCAModoEnum.EventoSmart:
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