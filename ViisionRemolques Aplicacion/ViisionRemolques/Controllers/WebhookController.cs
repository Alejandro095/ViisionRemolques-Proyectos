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
        private static long _peticionesExitosasCount = 0;

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

                var p = evento.EventoAlarmaRecuentoPersonas;

                // --- VALIDACIÓN INLINE ---
                bool esValido = false;

                if (p != null && !string.IsNullOrWhiteSpace(p.Algoritmo))
                {
                    var alg = p.Algoritmo.Trim().ToUpperInvariant();

                    bool objOk = !string.IsNullOrWhiteSpace(p.ObjetivoDetectadoTipo);
                    bool algOk = !string.IsNullOrWhiteSpace(p.Algoritmo);
                    bool regIdOk = !string.IsNullOrWhiteSpace(p.RegionId);
                    bool coordsOk = !string.IsNullOrWhiteSpace(p.RegionCoordenadas);
                    bool valCausaOk = !string.IsNullOrWhiteSpace(p.ValorCausaEvento);
                    bool opCausaOk = !string.IsNullOrWhiteSpace(p.OperadorCausaEvento);
                    bool cantPersOk = !string.IsNullOrWhiteSpace(p.CantidadPersonas);
                    bool nivDenOk = !string.IsNullOrWhiteSpace(p.NivelDensidad);
                    bool nomNivDenOk = !string.IsNullOrWhiteSpace(p.NombreNivelDensidad);
                    bool dirCamOk = !string.IsNullOrWhiteSpace(p.DireccionCambioDensidad);

                    bool coordsNulo = string.IsNullOrWhiteSpace(p.RegionCoordenadas);
                    bool valCausaNulo = string.IsNullOrWhiteSpace(p.ValorCausaEvento);
                    bool opCausaNulo = string.IsNullOrWhiteSpace(p.OperadorCausaEvento);
                    bool cantPersNulo = string.IsNullOrWhiteSpace(p.CantidadPersonas);
                    bool nivDenNulo = string.IsNullOrWhiteSpace(p.NivelDensidad);
                    bool nomNivDenNulo = string.IsNullOrWhiteSpace(p.NombreNivelDensidad);
                    bool dirCamNulo = string.IsNullOrWhiteSpace(p.DireccionCambioDensidad);

                    switch (alg)
                    {
                        case "DSA":
                        case "PQA":
                            // Requeridos: ObjetivoDetectadoTipo, Algoritmo, RegionId, RegionCoordenadas, ValorCausaEvento, OperadorCausaEvento
                            // No permitidos: CantidadPersonas, NivelDensidad, NombreNivelDensidad, DireccionCambioDensidad
                            esValido = objOk && algOk && regIdOk && coordsOk && valCausaOk && opCausaOk &&
                                       cantPersNulo && nivDenNulo && nomNivDenNulo && dirCamNulo;
                            break;

                        case "PDC":
                        case "TIMING":
                            // Requeridos: ObjetivoDetectadoTipo, Algoritmo, RegionId, CantidadPersonas
                            // No permitidos: RegionCoordenadas, ValorCausaEvento, OperadorCausaEvento, NivelDensidad, NombreNivelDensidad, DireccionCambioDensidad
                            esValido = objOk && algOk && regIdOk && cantPersOk &&
                                       coordsNulo && valCausaNulo && opCausaNulo && nivDenNulo && nomNivDenNulo && dirCamNulo;
                            break;

                        case "TRIGGER":
                            // Requeridos: ObjetivoDetectadoTipo, Algoritmo, RegionId, CantidadPersonas, NivelDensidad, NombreNivelDensidad, DireccionCambioDensidad
                            // No permitidos: RegionCoordenadas, ValorCausaEvento, OperadorCausaEvento
                            esValido = objOk && algOk && regIdOk && cantPersOk && nivDenOk && nomNivDenOk && dirCamOk &&
                                       coordsNulo && valCausaNulo && opCausaNulo;
                            break;

                        default:
                            esValido = false;
                            break;
                    }
                }

                long totalExitosas = esValido
                    ? System.Threading.Interlocked.Increment(ref _peticionesExitosasCount)
                    : System.Threading.Interlocked.Read(ref _peticionesExitosasCount);

                // Coloca tu Breakpoint aquí para inspeccionar 'esValido' y 'totalExitosas'
                _logger.LogInformation(
                    "Resultado de validación para Algoritmo '{Algoritmo}': {EsValido} | Total correctamente parseadas: {TotalExitosas}",
                    p?.Algoritmo ?? "NULL",
                    esValido,
                    totalExitosas);
                if (esValido == false)
                {
                    var quePaso = "Hola?";
                }

                switch (evento.BaseInfo.VCAModo)
                {
                    case VCAModoEnum.EventoSmart:

                        await _repo.InsertarAsync(new EventoPerimetral()
                        {
                            IPCamara = evento.BaseInfo.IpAddress,
                            Evento = evento.BaseInfo.EventType ?? "",

                            RegionId = evento?.EventoSmart?.RegionID,
                            ZonaDeteccion = evento?.EventoSmart?.RegionCoordenadas,
                            TipoObjetivo = evento?.EventoSmart?.ObjetivoDetectadoTipo,

                            FechaEvento = DateTime.Now,
                            PathImagen = imagenesPaths.Count != 0 ? imagenesPaths[0] : null,

                            Prioridad = (evento?.BaseInfo.EventType ?? "").Trim().ToLower() == "vmd" ? 10 : 5
                        });
                        break;
                    case VCAModoEnum.RecuentoPersonas:
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
                    case VCAModoEnum.AlarmaRecuentoPersonas:
                        var eventoAlarmaRecuentoPersoas = evento.EventoAlarmaRecuentoPersonas;

                        var h = 1;
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