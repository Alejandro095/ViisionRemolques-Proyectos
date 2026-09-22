using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Yarp.ReverseProxy.Transforms;
using Yarp.ReverseProxy.Transforms.Builder;

namespace ViisionRemolques.Auth.YARP
{
    public class QueryStringValidationTransformProvider : ITransformProvider
    {
        private static readonly JwtSecurityTokenHandler _tokenHandler = new();
        private readonly IConfiguration _configuration;
        private const string VideoSessionCookieName = "video_session_src";

        public QueryStringValidationTransformProvider(IConfiguration configuration)
        {
            _configuration = configuration;
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
                        cameraSrc = ExtraerCamaraDelToken(token);

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
                            Secure = true,
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

        private string? ExtraerCamaraDelToken(string token)
        {
            try
            {
                var keyBytes = Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]!);

                var validationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(keyBytes),
                    ValidateIssuer = true,
                    ValidIssuer = _configuration["Jwt:Issuer"],
                    ValidateAudience = false,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(10)
                };

                // Validar firma y vigencia del JWT
                var principal = _tokenHandler.ValidateToken(token, validationParameters, out SecurityToken validatedToken);

                // Extraer el claim "src" o "allowed_camera"
                var srcClaim = principal.FindFirst("src")?.Value
                            ?? principal.FindFirst("allowed_camera")?.Value;

                return srcClaim;
            }
            catch
            {
                // Firma inválida, token expirado o malformado
                return null;
            }
        }
    }
}