using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Net.Http.Headers;
using System.Net.Http.Headers;
using ViisionRemolques.Entities;
using ViisionRemolques.Enums;
using ViisionRemolques.Parsing;
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
        private readonly EventoPerimetralRepository _repo;
        private readonly EventoAlertaConteoPersonaRepository _eventoAlertaConteoPersonaRepository;
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<WebhookController> _logger;

        private readonly AlmacenamientoImagenesService _almacenamientoImagenesService;
        private readonly WebhookPayloadExtractorService _webhookPayloadExtractorService;

        public WebhookController(
            EventoPerimetralRepository repo,
            EventoAlertaConteoPersonaRepository eventoAlertaConteoPersonaRepository,
            IWebHostEnvironment env,
            ILogger<WebhookController> logger,
            AlarmaDesconocidaLogRepository alarmasDesconocidasLogRepository,
            AlmacenamientoImagenesService almacenamientoImagenesService,
            WebhookPayloadExtractorService webhookPayloadExtractorService)
        {
            _repo = repo;
            _env = env;
            _logger = logger;
            _alarmaDesconocidaLogRepository = alarmasDesconocidasLogRepository;
            _almacenamientoImagenesService = almacenamientoImagenesService;
            _webhookPayloadExtractorService = webhookPayloadExtractorService;
            _eventoAlertaConteoPersonaRepository = eventoAlertaConteoPersonaRepository;
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

                CameraEventModel? evento = CameraEventParser.Parse(webhookPayload.Body);

                var imagenesPaths = await _almacenamientoImagenesService.Guardar(webhookPayload.Imagenes);

                if (evento is not null && (
                    evento.BaseInfo.EventType == "heartBeat" || 
                    evento.BaseInfo.EventType == "duration"))
                {
                    return Ok();
                }

                if (evento is null || evento.BaseInfo.VCAModo == VCAModoEnum.Ninguno)
                {
                    await _alarmaDesconocidaLogRepository.InsertarAsync(new AlarmaDesonocidaLogEntity()
                    {
                        IPCamara = HttpContext.Connection.RemoteIpAddress?.ToString(),
                        ContentType = Request.ContentType,
                        Evento = evento?.BaseInfo.EventType ?? null,
                        Body = webhookPayload.Body,
                        Motivo = evento is null ? "PARSEO_ERROR" : "EXTRACTOR_DETALLES_ERROR"
                    });

                    return Ok();
                }

                switch (evento.BaseInfo.VCAModo)
                {
                    case VCAModoEnum.EventoSmart:

                        await _repo.InsertarAsync(new EventoPerimetral()
                        {
                            IPCamara = evento.BaseInfo.IpAddress,
                            Evento = evento.BaseInfo.EventType ?? "",

                            RegionId = evento?.EventoSmart?.RegionID,
                            ZonaDeteccion = evento?.EventoSmart?.RegionCoordinatesList,
                            TipoObjetivo = evento?.EventoSmart?.DetectionTarget,

                            FechaEvento = DateTime.Now,
                            PathImagen = imagenesPaths.Count != 0 ? imagenesPaths[0] : null,

                            Prioridad = (evento?.BaseInfo.EventType ?? "").Trim().ToLower() == "vmd" ? 10 : 5
                        });
                        break;
                    case VCAModoEnum.AlarmaConteoPersonas:
                        await _eventoAlertaConteoPersonaRepository.InsertarAsync(new EventoAlertaConteoPersonaEntity()
                        {
                            IPCamara = evento.BaseInfo.IpAddress,
                            Evento = evento.BaseInfo.EventType ?? "",

                            Regiones = evento.AlarmaConteoPersonas.Regiones,
                            TotalEntradas = evento?.AlarmaConteoPersonas.TotalEntradas ?? "0",
                            TotalSalidas = evento?.AlarmaConteoPersonas.TotalSalidas ?? "0",
                            TotalPasos = evento?.AlarmaConteoPersonas.TotalPasos ?? "0",
                            TotalDuplicados = evento?.AlarmaConteoPersonas.TotalDuplicados ?? "0",

                            FechaEvento = DateTime.Now,
                            Prioridad = 5
                        });
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