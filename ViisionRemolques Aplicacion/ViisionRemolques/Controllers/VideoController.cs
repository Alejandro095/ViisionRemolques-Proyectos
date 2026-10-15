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
    [ApiController]
    [Route("/video")]
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

        [HttpPost]
        [Route("token/stream")]
        public async Task<IActionResult> Autorizacion([FromQuery] long idInterno)
        {
            var camara = await _camaraRepository.ObtenerPorIdInternoAsync(idInterno);

            if (camara == null || string.IsNullOrWhiteSpace(camara.Go2Rtc))
            {
                return NotFound(new { message = $"No se encontró la cámara o el identificador go2rtc para el idInterno: {idInterno}" });
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

            return Ok(new
            {
                Id = camara.IdInterno,
                modelo = camara.Modelo,

                autorizacion = new {
                    token = tokenString,
                    expiraEn = 300,
                },
                
                path = $"/v1/api/video/streaming/stream.html?token={tokenString}",
            });
        }

        public record ClipRequest(int Canal, string Fecha);

        [HttpPost]
        [Route("token/clip")]
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
                return NotFound(new { message = $"No se encontró grabación en el canal {request.Canal} para el instante solicitado." });

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

            return Ok(new
            {
                canal = request.Canal,
                autorizacion = new
                {
                    token = tokenString,
                    expiraEn = 300,
                },

                path = $"/v1/api/video/streaming/stream.html?token={tokenString}",
            });
        }
    }
}