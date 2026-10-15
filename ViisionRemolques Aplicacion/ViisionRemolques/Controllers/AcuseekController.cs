using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using ViisionRemolques.Services.ISAPI;

namespace ViisionRemolques.Controllers
{
    [ApiController]
    [Route("/acuseek")]
    public class AcuseekController : ControllerBase
    {
        private readonly AcuseekService _acuseekService;

        public AcuseekController(AcuseekService acuseekService)
        {
            _acuseekService = acuseekService;
        }

        [HttpGet]
        [Route("hora")]
        public async Task<IActionResult> ObtenerHora(CancellationToken ct)
        {
            var resultado = await _acuseekService.ObtenerHoraDispositivoAsync(ct);
            if (!resultado.Exito)
                return ErrorMvr(resultado.Error);

            return Ok(new { hora = resultado.Valor.Hora, offset = resultado.Valor.Offset });
        }

        [HttpGet]
        [Route("canales")]
        public async Task<IActionResult> ObtenerCanales(CancellationToken ct)
        {
            var resultado = await _acuseekService.ObtenerCanalesAsync(ct);
            if (!resultado.Exito)
                return ErrorMvr(resultado.Error);

            return Ok(resultado.Valor!.Select(c => new { id = c.Id, nombre = c.Nombre }));
        }

        [HttpGet]
        [Route("historial")]
        public async Task<IActionResult> ObtenerHistorial(CancellationToken ct)
        {
            var resultado = await _acuseekService.ObtenerHistorialAsync(ct);
            if (!resultado.Exito)
                return ErrorMvr(resultado.Error);

            return Ok(resultado.Valor);
        }

        [HttpGet]
        [Route("favoritos")]
        public async Task<IActionResult> ObtenerFavoritos(CancellationToken ct)
        {
            var resultado = await _acuseekService.ObtenerFavoritosAsync(ct);
            if (!resultado.Exito)
                return ErrorMvr(resultado.Error);

            return Ok(resultado.Valor);
        }

        public record FavoritoRequest(string Terminos, bool Agregar);

        [HttpPost]
        [Route("favoritos")]
        public async Task<IActionResult> EstablecerFavorito([FromBody] FavoritoRequest request, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(request.Terminos))
                return Problem(
                    detail: "El parámetro 'Terminos' es obligatorio.",
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Parámetros inválidos"
                );

            var resultado = await _acuseekService.EstablecerFavoritoAsync(request.Terminos, request.Agregar, ct);
            if (!resultado.Exito)
                return ErrorMvr(resultado.Error);

            return Ok();
        }

        public record BusquedaRequest(string Texto, string? Rango, string? Start, string? End, int[]? Canales, int Similarity, string? Idioma);

        [HttpPost]
        [Route("busqueda")]
        public async Task<IActionResult> IniciarBusqueda([FromBody] BusquedaRequest request, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(request.Texto))
                return Problem(
                    detail: "El parámetro 'Texto' es obligatorio.",
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Parámetros inválidos"
                );

            var horaResultado = await _acuseekService.ObtenerHoraDispositivoAsync(ct);
            if (!horaResultado.Exito)
                return ErrorMvr(horaResultado.Error);

            var (startTime, endTime) = ConstruirRango(
                request.Rango, request.Start, request.End, horaResultado.Valor.Hora, horaResultado.Valor.Offset);

            int[] canales;
            if (request.Canales is { Length: > 0 })
            {
                canales = request.Canales;
            }
            else
            {
                var canalesResultado = await _acuseekService.ObtenerCanalesAsync(ct);
                if (!canalesResultado.Exito)
                    return ErrorMvr(canalesResultado.Error);

                canales = canalesResultado.Valor!.Select(c => c.Id).ToArray();
            }

            var resultado = await _acuseekService.IniciarBusquedaTextoAsync(
                request.Texto, startTime, endTime, canales, request.Similarity, request.Idioma, ct);

            if (!resultado.Exito)
                return ErrorMvr(resultado.Error);

            return Ok(new { taskId = resultado.Valor });
        }

        public record EstadoBusquedaRequest(string TaskId, int SearchResultPosition, int MaxResults, int Similarity);

        [HttpPost]
        [Route("busqueda/estado")]
        public async Task<IActionResult> ObtenerEstadoBusqueda([FromBody] EstadoBusquedaRequest request, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(request.TaskId))
                return Problem(
                    detail: "El parámetro 'TaskId' es obligatorio.",
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Parámetros inválidos"
                );

            var resultado = await _acuseekService.ObtenerEstadoBusquedaAsync(
                request.TaskId, request.SearchResultPosition, request.MaxResults, request.Similarity, ct);

            if (!resultado.Exito)
                return ErrorMvr(resultado.Error);

            return Content(resultado.Valor!, "application/json");
        }

        [HttpDelete]
        [Route("busqueda/{taskId}")]
        public async Task<IActionResult> EliminarBusqueda(string taskId, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(taskId))
                return Problem(
                    detail: "El parámetro 'taskId' es obligatorio.",
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Parámetros inválidos"
                );

            var resultado = await _acuseekService.EliminarBusquedaAsync(taskId, ct);
            if (!resultado.Exito)
                return ErrorMvr(resultado.Error);

            return Ok();
        }

        public record TuningSubmitRequest(string Texto, string[]? Positivas, string[]? Negativas, string? Idioma);

        [HttpPost]
        [Route("tuning")]
        public async Task<IActionResult> EnviarTuning([FromBody] TuningSubmitRequest request, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(request.Texto))
                return Problem(
                    detail: "El parámetro 'Texto' es obligatorio.",
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Parámetros inválidos"
                );

            var resultado = await _acuseekService.EnviarTuningAsync(
                request.Texto, request.Positivas ?? Array.Empty<string>(), request.Negativas ?? Array.Empty<string>(), request.Idioma, ct);

            if (!resultado.Exito)
                return ErrorMvr(resultado.Error);

            return Ok(new { resultId = resultado.Valor });
        }

        [HttpGet]
        [Route("tuning/estado")]
        public async Task<IActionResult> ObtenerEstadoTuning([FromQuery] string texto, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(texto))
                return Problem(
                    detail: "El parámetro 'texto' es obligatorio.",
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Parámetros inválidos"
                );

            var resultado = await _acuseekService.ObtenerEstadoTuningAsync(texto, ct);
            if (!resultado.Exito)
                return ErrorMvr(resultado.Error);

            return Content(resultado.Valor!, "application/json");
        }

        [HttpGet]
        [Route("imagen")]
        public async Task<IActionResult> ObtenerImagen([FromQuery] string url, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(url))
                return Problem(
                    detail: "El parámetro 'url' es obligatorio.",
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Parámetros inválidos"
                );

            var resultado = await _acuseekService.ObtenerImagenAsync(url, ct);
            if (!resultado.Exito)
                return ErrorMvr(resultado.Error);

            return File(resultado.Valor.Bytes, resultado.Valor.ContentType);
        }

        // ---------------- helpers ----------------

        private IActionResult ErrorMvr(string? error) =>
            Problem(
                detail: error ?? "No fue posible comunicarse con el MVR.",
                statusCode: StatusCodes.Status502BadGateway,
                title: "Error al consultar el MVR"
            );

        private static (string start, string end) ConstruirRango(string? rango, string? start, string? end, DateTime now, string offset)
        {
            string Fmt(DateTime d) => d.ToString("yyyy-MM-ddTHH:mm:ss") + offset;

            var hoy = now.Date;
            return (rango ?? "today").ToLowerInvariant() switch
            {
                "3d" => (Fmt(hoy.AddDays(-2)), Fmt(hoy.AddDays(1).AddSeconds(-1))),
                "7d" => (Fmt(hoy.AddDays(-6)), Fmt(hoy.AddDays(1).AddSeconds(-1))),
                "custom" => (Fmt(ParseLocal(start) ?? hoy), Fmt(ParseLocal(end) ?? hoy.AddDays(1).AddSeconds(-1))),
                _ => (Fmt(hoy), Fmt(hoy.AddDays(1).AddSeconds(-1))),
            };
        }

        private static DateTime? ParseLocal(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            return DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;
        }
    }
}
