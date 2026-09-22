using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Text;
using ViisionRemolques.Repositories;

namespace ViisionRemolques.Controllers
{
    [ApiController]
    [Route("/video")]
    public class VideoController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly CamaraRepository _camaraRepository;

        public VideoController(IConfiguration configuration, CamaraRepository camaraRepository)
        {
            _configuration = configuration;
            _camaraRepository = camaraRepository;
        }

        [HttpGet]
        [Route("autorizacion")]
        public async Task<IActionResult> Autorizacion([FromQuery] long idInterno)
        {
            // 1. Obtener la cámara desde el repositorio
            var camara = await _camaraRepository.ObtenerPorIdInternoAsync(idInterno);

            if (camara == null || string.IsNullOrWhiteSpace(camara.Go2Rtc))
            {
                return NotFound(new { message = $"No se encontró la cámara o el identificador go2rtc para el idInterno: {idInterno}" });
            }

            var go2rtcSrc = camara.Go2Rtc;

            // 2. Extraer el usuario o asignar valor por defecto
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                         ?? User.FindFirst("sub")?.Value
                         ?? "anonymous";

            // 3. Incluir la propiedad Go2Rtc como Claim explícito
            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, userId),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim("scope", "video_stream"),
                new Claim("src", go2rtcSrc), // Claim 'src' esperado por YARP
                new Claim("allowed_camera", go2rtcSrc)
            };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]!));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _configuration["Jwt:Issuer"],
                audience: "ViisionVideoApi",
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(5),
                signingCredentials: creds
            );

            var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

            var streamUrl = $"{Request.Scheme}://{Request.Host}/v1/api/video/streaming/stream.html?token={tokenString}";

            return Ok(new
            {
                Id = camara.IdInterno,
                modelo = camara.Modelo,

                autorizacion = new {
                    token = tokenString,
                    expiraEn = 300,
                },
                
                streamUrl = streamUrl,
            });
        }
    }
}