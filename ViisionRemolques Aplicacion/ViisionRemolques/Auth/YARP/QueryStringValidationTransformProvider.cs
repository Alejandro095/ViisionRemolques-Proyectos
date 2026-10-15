using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using ViisionRemolques.Settings;
using Yarp.ReverseProxy.Transforms;
using Yarp.ReverseProxy.Transforms.Builder;

namespace ViisionRemolques.Auth.YARP
{
    public class QueryStringValidationTransformProvider : ITransformProvider
    {
        private static readonly JwtSecurityTokenHandler _tokenHandler = new();
        private readonly MvrSettings _mvrSettings;
        private readonly JwtSettings _jwtSettings;
        private const string VideoSessionCookieName = "video_session_src";

        public QueryStringValidationTransformProvider(IOptions<MvrSettings> mvrSettings, IOptions<JwtSettings> jwtSettings)
        {
            _mvrSettings = mvrSettings.Value;
            _jwtSettings = jwtSettings.Value;
        }

        public void ValidateRoute(TransformRouteValidationContext context) { }
        public void ValidateCluster(TransformClusterValidationContext context) { }

        public void Apply(TransformBuilderContext context)
        {
            context.AddRequestTransform(async transformContext =>
            {
                var httpContext = transformContext.HttpContext;
                var path = httpContext.Request.Path;

                // Validar solo peticiones a /video/streaming
                if (path.StartsWithSegments("/video/streaming"))
                {
                    var query = httpContext.Request.Query;
                    var token = query["token"].ToString();
                    var sessionCookie = httpContext.Request.Cookies[VideoSessionCookieName];
                    string? cameraSrc = null;

                    // 1. Validar por Token JWT (Primera petición)
                    if (!string.IsNullOrWhiteSpace(token))
                    {
                        cameraSrc = ExtraerSrcDelToken(token);

                        if (string.IsNullOrEmpty(cameraSrc))
                        {
                            httpContext.Response.StatusCode = StatusCodes.Status401Unauthorized;
                            await httpContext.Response.CompleteAsync();
                            return;
                        }

                        // Guardar la cámara validada en una Cookie de sesión HTTP temporal (5 minutos)
                        // para soportar reconexiones y subpeticiones automáticas del reproductor go2rtc
                        httpContext.Response.Cookies.Append(VideoSessionCookieName, cameraSrc, new CookieOptions
                        {
                            HttpOnly = true,
                            Secure = httpContext.Request.IsHttps,
                            SameSite = SameSiteMode.Lax,
                            Expires = DateTimeOffset.UtcNow.AddMinutes(5)
                        });
                    }
                    // 2. Si no hay token en la Query, intentar recuperar la sesión desde la Cookie
                    else if (!string.IsNullOrWhiteSpace(sessionCookie))
                    {
                        cameraSrc = sessionCookie;
                    }
                    // 3. Si no hay ni token ni cookie válida, rechazar petición
                    else
                    {
                        httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
                        await httpContext.Response.CompleteAsync();
                        return;
                    }

                    // 4. Modificar la Query String enviada a go2rtc:
                    // Se remueve 'token' para no enviarlo a go2rtc y se inyecta 'src' con la cámara validada
                    transformContext.Query.Collection.Remove("token");
                    transformContext.Query.Collection["src"] = cameraSrc;
                }
            });
        }

        private string? ExtraerSrcDelToken(string token)
        {
            try
            {
                var keyBytes = Encoding.UTF8.GetBytes(_jwtSettings.Key);

                var validationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(keyBytes),
                    ValidateIssuer = true,
                    ValidIssuer = _jwtSettings.Issuer,
                    ValidateAudience = false,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(10)
                };

                // Validar firma y vigencia del JWT
                var principal = _tokenHandler.ValidateToken(token, validationParameters, out SecurityToken validatedToken);

                var scope = principal.FindFirst("scope")?.Value;

                // Clip grabado del MVR: el token solo lleva canal + ventana de tiempo;
                // la URL RTSP (con credenciales del MVR) se arma aquí, nunca llega al cliente.
                if (scope == "video_clip")
                    return ConstruirSrcClipMVR(principal);

                // Streaming en vivo: el claim ya trae el nombre del stream go2rtc.
                return principal.FindFirst("src")?.Value
                    ?? principal.FindFirst("allowed_camera")?.Value;
            }
            catch
            {
                // Firma inválida, token expirado o malformado
                return null;
            }
        }

        private string? ConstruirSrcClipMVR(ClaimsPrincipal principal)
        {
            var canalClaim = principal.FindFirst("mvr_canal")?.Value;
            var inicio = principal.FindFirst("mvr_inicio")?.Value;
            var fin = principal.FindFirst("mvr_fin")?.Value;

            if (string.IsNullOrWhiteSpace(canalClaim) || string.IsNullOrWhiteSpace(inicio) || string.IsNullOrWhiteSpace(fin))
                return null;

            if (!int.TryParse(canalClaim, out var canal) || canal <= 0)
                return null;

            var ip = _mvrSettings.IP;
            var usuario = _mvrSettings.Usuario;
            var secreto = _mvrSettings.Secreto;
            var puerto = _mvrSettings.RtspPort;

            if (string.IsNullOrWhiteSpace(ip) || string.IsNullOrWhiteSpace(usuario) || string.IsNullOrWhiteSpace(secreto))
                return null;

            // Pista de video principal del canal: id*100+1 (convención ISAPI de Hikvision).
            var trackId = canal * 100 + 1;
            var user = Uri.EscapeDataString(usuario);
            var pass = Uri.EscapeDataString(secreto);
            var rtsp = $"rtsp://{user}:{pass}@{ip}:{puerto}/Streaming/tracks/{trackId}?starttime={inicio}&endtime={fin}";

            // El RTSP de grabación del MVR trae timestamps que atascan el jitter buffer del
            // navegador; enrutar por ffmpeg regenera los PTS.
            return $"ffmpeg:{rtsp}#video=h264";
        }
    }
}