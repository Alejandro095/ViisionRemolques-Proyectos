using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Text;
using ViisionRemolques.Repositories;
using ViisionRemolques.Services.ISAPI;
using ViisionRemolques.Settings;

namespace ViisionRemolques.Controllers
{
    /// <summary>
    /// Emisión de tokens de video: acceso al streaming en vivo de una cámara y a clips grabados en el MVR.
    /// </summary>
    /// <remarks>
    /// Estos endpoints no devuelven video: devuelven un token JWT de corta duración y la ruta del reproductor.
    /// El video se sirve a través del proxy interno <c>/v1/api/video/streaming/...</c>, que valida el token y
    /// resuelve por sí mismo el origen real (nombre del stream go2rtc o URL RTSP del MVR con credenciales).
    /// Ni la URL RTSP ni las credenciales del MVR se exponen nunca al cliente.
    ///
    /// Flujo de uso: pedir el token al endpoint correspondiente, abrir la <c>path</c> devuelta en un iframe o
    /// navegador y, si el token caduca durante la sesión, volver a pedir uno nuevo.
    /// </remarks>
    [ApiController]
    [Route("/video")]
    [Produces("application/json")]
    public class VideoController : ControllerBase
    {
        private readonly CamaraRepository _camaraRepository;
        private readonly MvrSettings _mvrSettings;
        private readonly JwtSettings _jwtSettings;
        private readonly MvrService _mvrService;

        public VideoController(CamaraRepository camaraRepository, IOptions<MvrSettings> mvrSettings, IOptions<JwtSettings> jwtSettings, MvrService mvrService)
        {
            _camaraRepository = camaraRepository;
            _mvrSettings = mvrSettings.Value;
            _jwtSettings = jwtSettings.Value;
            _mvrService = mvrService;
        }

        /// <summary>
        /// Token de acceso al video y su vigencia.
        /// </summary>
        /// <param name="Token">JWT firmado que autoriza el acceso al proxy de video. Se envía como query string <c>?token=</c>.</param>
        /// <param name="ExpiraEn">Vigencia del token en segundos desde su emisión (300 = 5 minutos).</param>
        public record AutorizacionResponse(string Token, int ExpiraEn);

        /// <summary>
        /// Autorización para ver el streaming en vivo de una cámara.
        /// </summary>
        /// <param name="Id">Identificador interno de la cámara autorizada.</param>
        /// <param name="Modelo">Modelo del fabricante de la cámara.</param>
        /// <param name="Autorizacion">Token emitido y su vigencia.</param>
        /// <param name="Path">Ruta relativa del reproductor con el token ya incluido. Debe abrirse tal cual, sin modificar la query string.</param>
        public record StreamTokenResponse(
            long Id,
            string Modelo,
            AutorizacionResponse Autorizacion,
            string Path
        );

        /// <summary>
        /// Autorización para reproducir un clip grabado del MVR.
        /// </summary>
        /// <param name="Canal">Canal del MVR al que corresponde el clip.</param>
        /// <param name="Autorizacion">Token emitido y su vigencia.</param>
        /// <param name="Path">Ruta relativa del reproductor con el token ya incluido. Debe abrirse tal cual, sin modificar la query string.</param>
        public record ClipTokenResponse(
            int Canal,
            AutorizacionResponse Autorizacion,
            string Path
        );

        /// <summary>
        /// Mensaje de error simple devuelto por los endpoints de video.
        /// </summary>
        /// <param name="Message">Descripción del motivo del error.</param>
        public record MensajeResponse(string Message);

        /// <summary>
        /// Emite un token para ver el streaming en vivo de una cámara.
        /// </summary>
        /// <remarks>
        /// Devuelve un JWT de 5 minutos y la ruta del reproductor web ya firmada. El token queda ligado a una sola
        /// cámara: el proxy inyecta internamente el stream autorizado y descarta cualquier intento de cambiarlo.
        ///
        /// Consideraciones y limitaciones:
        /// - La cámara debe existir, estar activa y tener configurado su identificador de stream go2rtc. Si falta
        ///   cualquiera de las dos cosas se responde 404; una cámara registrada pero sin go2rtc no se puede ver.
        /// - El token vive 5 minutos (<c>expiraEn: 300</c>) y no es renovable: para seguir viendo después hay que
        ///   solicitar uno nuevo.
        /// - Al abrir la <c>path</c>, el proxy valida el token y deja una cookie de sesión <c>video_session_src</c>
        ///   (HttpOnly, 5 minutos) para que las reconexiones y subpeticiones del reproductor no necesiten volver a
        ///   enviar el token. Por eso la ruta debe abrirse en un contexto que acepte cookies.
        /// - La <c>path</c> es relativa al host de la API y debe usarse sin alterar: quitar o cambiar el parámetro
        ///   <c>token</c> hace que el proxy responda 400 o 401.
        /// - El token autoriza únicamente el streaming en vivo (ámbito <c>video_stream</c>); no sirve para clips.
        /// - La emisión no verifica que la cámara esté en línea: solo valida el registro. Si el dispositivo está
        ///   caído, el token se emite igual y el fallo aparecerá al reproducir.
        /// - El identificador del usuario se toma del bearer token de la petición si existe; en su defecto el token
        ///   se emite a nombre de <c>anonymous</c>.
        /// </remarks>
        /// <param name="idInterno">Identificador interno de la cámara, obtenido de <c>GET /camaras</c>.</param>
        /// <response code="200">Token emitido correctamente junto con la ruta del reproductor.</response>
        /// <response code="404">No existe una cámara activa con ese identificador, o no tiene configurado el stream go2rtc.</response>
        [HttpPost]
        [Route("token/stream")]
        [ProducesResponseType(typeof(StreamTokenResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(MensajeResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Autorizacion([FromQuery] long idInterno)
        {
            var camara = await _camaraRepository.ObtenerPorIdInternoAsync(idInterno);

            if (camara == null || string.IsNullOrWhiteSpace(camara.Go2Rtc))
            {
                return NotFound(new MensajeResponse($"No se encontró la cámara o el identificador go2rtc para el idInterno: {idInterno}"));
            }

            var go2rtcSrc = camara.Go2Rtc;

            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                         ?? User.FindFirst("sub")?.Value
                         ?? "anonymous";

            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, userId),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim("scope", "video_stream"),
                new Claim("src", go2rtcSrc),
                new Claim("allowed_camera", go2rtcSrc)
            };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.Key));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _jwtSettings.Issuer,
                audience: _jwtSettings.Audience,
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(5),
                signingCredentials: creds
            );

            var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

            return Ok(new StreamTokenResponse(
                Id: camara.IdInterno,
                Modelo: camara.Modelo,

                Autorizacion: new AutorizacionResponse(
                    Token: tokenString,
                    ExpiraEn: 300
                ),

                Path: $"/v1/api/video/streaming/stream.html?token={tokenString}"
            ));
        }

        /// <summary>
        /// Solicitud de clip grabado alrededor de un instante concreto.
        /// </summary>
        /// <param name="Canal">Canal del MVR a consultar. Debe ser un entero positivo (corresponde al <c>id</c> de <c>GET /acuseek/canales</c>).</param>
        /// <param name="Fecha">Instante de interés en formato ISO 8601 (p. ej. <c>2026-10-14T20:37:02-06:00</c>). El clip se construye alrededor de esta fecha, no a partir de ella.</param>
        public record ClipRequest(int Canal, string Fecha);

        /// <summary>
        /// Emite un token para reproducir un clip grabado en el MVR alrededor de un instante.
        /// </summary>
        /// <remarks>
        /// Pensado para "ver la grabación de este evento": a partir de una fecha, la plataforma calcula una ventana
        /// de video, comprueba contra el MVR que exista grabación y emite un JWT de 5 minutos con la ruta del
        /// reproductor.
        ///
        /// Consideraciones y limitaciones:
        /// - La ventana del clip no se elige en la petición: se calcula como la fecha indicada menos los segundos
        ///   previos y más los segundos posteriores definidos en la configuración del MVR (por defecto 10 segundos
        ///   antes y 15 después). Para una ventana distinta hay que cambiar la configuración del servidor.
        /// - Antes de emitir el token se consulta el MVR para confirmar que hay grabación en ese canal y ventana.
        ///   Si no hay nada grabado se responde 404 y no se emite token.
        /// - Si el MVR no responde o devuelve una respuesta inválida se responde 502: es un fallo del equipo de
        ///   grabación, no de la petición.
        /// - <c>Fecha</c> debe ser una fecha/hora válida en formato ISO 8601; se recomienda incluir el desplazamiento
        ///   horario del dispositivo (<c>GET /acuseek/hora</c> devuelve la hora y el offset del MVR). Un valor
        ///   inválido responde 400.
        /// - <c>Canal</c> debe ser positivo; se traduce internamente a la pista de video principal del canal.
        /// - El token vive 5 minutos (<c>expiraEn: 300</c>) y queda ligado al canal y a la ventana calculada: no
        ///   permite reproducir otro canal ni otro intervalo. Caducado el token, hay que volver a solicitarlo.
        /// - El token autoriza únicamente la reproducción de clips (ámbito <c>video_clip</c>); no sirve para el
        ///   streaming en vivo.
        /// - La <c>path</c> debe abrirse sin modificar su query string, y en un contexto que acepte cookies: el
        ///   proxy deja la cookie de sesión <c>video_session_src</c> para las reconexiones del reproductor.
        /// - La reproducción de grabaciones se reenvía por ffmpeg para regenerar las marcas de tiempo del MVR, por
        ///   lo que el primer fotograma puede tardar un poco más que en el video en vivo.
        /// - El identificador del usuario se toma del bearer token de la petición si existe; en su defecto el token
        ///   se emite a nombre de <c>anonymous</c>.
        /// </remarks>
        /// <param name="request">Canal y fecha del instante a reproducir.</param>
        /// <response code="200">Token emitido correctamente junto con la ruta del reproductor.</response>
        /// <response code="400">El canal no es positivo o la fecha no es una fecha/hora válida.</response>
        /// <response code="404">No existe grabación en ese canal para el instante solicitado.</response>
        /// <response code="502">No fue posible consultar las grabaciones del MVR.</response>
        [HttpPost]
        [Route("token/clip")]
        [ProducesResponseType(typeof(ClipTokenResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(MensajeResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status502BadGateway)]
        public async Task<IActionResult> Clip([FromBody] ClipRequest request)
        {
            if (request.Canal <= 0)
                return Problem(
                    detail: "El parámetro 'Canal' debe ser un valor positivo.",
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Parámetros inválidos"
                );

            if (!DateTimeOffset.TryParse(request.Fecha, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var fecha))
                return Problem(
                    detail: "El parámetro 'Fecha' no es una fecha/hora válida.",
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Parámetros inválidos"
                );

            var inicio = fecha.AddSeconds(-_mvrSettings.ClipPreSegundos);
            var fin = fecha.AddSeconds(_mvrSettings.ClipPostSegundos);

            var existeGrabacion = await _mvrService.ExisteGrabacionAsync(request.Canal, inicio, fin, HttpContext.RequestAborted);

            if (!existeGrabacion.Exito)
                return Problem(
                    detail: existeGrabacion.Error ?? "No fue posible consultar las grabaciones del MVR.",
                    statusCode: StatusCodes.Status502BadGateway,
                    title: "Error al consultar el MVR"
                );

            if (!existeGrabacion.Valor)
                return NotFound(new MensajeResponse($"No se encontró grabación en el canal {request.Canal} para el instante solicitado."));

            // El MVR espera hora LOCAL del dispositivo con sufijo 'Z' (no es UTC realmente).
            static string FormatoMVR(DateTimeOffset d) =>
                d.ToString("yyyyMMdd'T'HHmmss", CultureInfo.InvariantCulture) + "Z";

            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                         ?? User.FindFirst("sub")?.Value
                         ?? "anonymous";

            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, userId),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim("scope", "video_clip"),
                new Claim("mvr_canal", request.Canal.ToString()),
                new Claim("mvr_inicio", FormatoMVR(inicio)),
                new Claim("mvr_fin", FormatoMVR(fin))
            };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.Key));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _jwtSettings.Issuer,
                audience: _jwtSettings.Audience,
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(5),
                signingCredentials: creds
            );

            var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

            return Ok(new ClipTokenResponse(
                Canal: request.Canal,

                Autorizacion: new AutorizacionResponse(
                    Token: tokenString,
                    ExpiraEn: 300
                ),

                Path: $"/v1/api/video/streaming/stream.html?token={tokenString}"
            ));
        }
    }
}
