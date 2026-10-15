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
    /// <summary>
    /// Punto de recepción de notificaciones de eventos enviadas por las cámaras (HTTP Listening de ISAPI).
    /// </summary>
    /// <remarks>
    /// Este controlador no está diseñado para ser consumido por aplicaciones cliente: es la URL que se configura
    /// en las cámaras como destino de sus notificaciones. Se documenta para poder diagnosticar la integración y
    /// reproducir payloads durante pruebas.
    /// </remarks>
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

        /// <summary>
        /// Recibe un evento enviado por una cámara, guarda sus imágenes y encola su procesamiento.
        /// </summary>
        /// <remarks>
        /// Receptor de eventos de analítica de las cámaras. Está optimizado para liberar la conexión lo antes
        /// posible: guarda las imágenes adjuntas en disco, encola un trabajo en segundo plano con el evento y
        /// responde de inmediato. La persistencia en base de datos y el reenvío ocurren fuera de esta petición,
        /// por lo que un 200 significa "evento aceptado para procesar", no "evento almacenado".
        ///
        /// Formato de la petición:
        /// - Admite <c>multipart/form-data</c> (el formato habitual de las cámaras: una parte con el XML/JSON del
        ///   evento y una o varias partes de imagen) y también un cuerpo plano con el XML o JSON del evento.
        /// - En <c>multipart</c>, las partes con <c>Content-Type</c> de tipo <c>image/*</c> se tratan como
        ///   imágenes del evento; cualquier otra parte se interpreta como el cuerpo del evento (se usa la primera
        ///   que llegue con contenido).
        /// - No tiene límite de tamaño de petición, para admitir eventos con varias imágenes de alta resolución.
        ///
        /// Consideraciones y limitaciones:
        /// - Responde 200 incluso ante errores internos. Si el procesamiento falla, la excepción se registra en el
        ///   log con nivel crítico pero la cámara recibe 200 para que no reintente ni acumule reenvíos. El 200 no
        ///   garantiza que el evento se haya interpretado correctamente.
        /// - Solo responde 400 cuando la petición no trae ningún cuerpo de evento identificable.
        /// - Los eventos de tipo <c>heartBeat</c> y <c>duration</c> se descartan intencionadamente: son señales de
        ///   vida o de fin de evento y no generan registro.
        /// - Los eventos cuyo tipo no corresponde a ningún modo VCA soportado también se descartan sin registrarse.
        ///   El tipo de evento que envía una cámara depende de su modo VCA configurado
        ///   (<c>GET /camaras/{idInterno}/vca/modo</c>).
        /// - Las imágenes se guardan en el almacenamiento local organizadas por año/mes/día (fecha UTC) con un
        ///   nombre aleatorio y extensión <c>.jpg</c>; solo se conserva la ruta en el evento procesado.
        /// - El procesamiento posterior se ejecuta en Hangfire, por lo que depende de que el servidor de trabajos
        ///   esté operativo. Los reintentos y el estado del trabajo se consultan en el panel <c>/hangfire</c>.
        /// - La respuesta no devuelve cuerpo ni identificador del evento: no hay forma de correlacionar la llamada
        ///   con el registro resultante desde este endpoint.
        /// </remarks>
        /// <param name="Jobs">Cliente de Hangfire inyectado por el framework; no forma parte de la petición.</param>
        /// <param name="cancellationToken">Token de cancelación gestionado por el framework.</param>
        /// <response code="200">Evento aceptado, descartado por tipo, o fallido internamente. Siempre sin cuerpo.</response>
        /// <response code="400">La petición no incluye un cuerpo de evento interpretable.</response>
        [HttpPost]
        [DisableRequestSizeLimit]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
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
