using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using ViisionRemolques.Enums;
using ViisionRemolques.Repositories;
using ViisionRemolques.Services.ISAPI;
using ViisionRemolques.Services.ISAPI.VCA;

namespace ViisionRemolques.Controllers
{
    /// <summary>
    /// Consulta y cambio del modo de analítica de video (VCA) de una cámara.
    /// </summary>
    /// <remarks>
    /// El modo VCA determina qué analítica ejecuta el dispositivo (ANPR, captura facial, recuento de personas, etc.)
    /// y, por lo tanto, qué tipo de eventos enviará al webhook. Una cámara solo puede tener un modo VCA activo a la vez.
    /// </remarks>
    [ApiController]
    [Route("camaras/{idInterno:long}/vca")]
    [Produces("application/json")]
    public class VCAController : ControllerBase
    {
        private readonly CamaraRepository _camaraRepository;
        private readonly VcaService _vcaService;
        public VCAController(CamaraRepository camaraRepository, VcaService vcaService)
        {
            _camaraRepository = camaraRepository;
            _vcaService = vcaService;
        }

        /// <summary>
        /// Modo VCA activo en la cámara.
        /// </summary>
        /// <param name="Vca">Identificador del modo activo (p. ej. <c>anpr</c>, <c>captura_facial</c>, <c>ninguno</c>).</param>
        public record ModoActualResponse(string Vca);

        /// <summary>
        /// Resultado del cambio de modo VCA.
        /// </summary>
        /// <param name="Mensaje">Texto informativo sobre el resultado y el reinicio del dispositivo.</param>
        /// <param name="Vca">Identificador del modo que quedó configurado.</param>
        public record CambioModoResponse(string Mensaje, string Vca);

        /// <summary>
        /// Modo VCA soportado por la cámara.
        /// </summary>
        /// <param name="Id">Identificador del modo; es el valor que se envía en <c>PUT /camaras/{idInterno}/vca/modo/{vca}</c>.</param>
        /// <param name="Titulo">Descripción legible del modo para mostrar en interfaz.</param>
        public record ModosVCA(string Id, string Titulo);

        /// <summary>
        /// Obtiene el modo VCA que la cámara tiene configurado en este momento.
        /// </summary>
        /// <remarks>
        /// Consulta en vivo al dispositivo vía ISAPI, por lo que refleja el estado real y no el último valor enviado
        /// desde la plataforma.
        ///
        /// Consideraciones y limitaciones:
        /// - La cámara debe existir y estar activa; en caso contrario se responde 404.
        /// - Requiere que la cámara tenga una plataforma VCA asignada. Si está registrada sin soporte de analítica
        ///   se responde 400 con el mensaje "La cámara (IP) no soporta VCA".
        /// - El valor devuelto puede ser <c>ninguno</c>, que indica que la cámara está operando sin analítica.
        /// - La operación depende de la red: si el dispositivo está apagado o inaccesible se responde 400 con el
        ///   detalle del error ISAPI (código HTTP y respuesta del dispositivo).
        /// - Es de solo lectura: no altera la configuración ni reinicia el dispositivo.
        /// </remarks>
        /// <param name="idInterno">Identificador interno de la cámara, obtenido de <c>GET /camaras</c>. Debe ser un entero positivo.</param>
        /// <param name="ct">Token de cancelación gestionado por el framework.</param>
        /// <response code="200">Modo VCA actualmente activo.</response>
        /// <response code="400">La cámara no soporta VCA o el dispositivo no respondió correctamente.</response>
        /// <response code="404">No existe una cámara activa con el identificador indicado.</response>
        [HttpGet]
        [Route("modo")]
        [ProducesResponseType(typeof(ModoActualResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> ObtenerModoActual(
            [Range(1, long.MaxValue, ErrorMessage = "El parámetro '{0}' debe ser un valor positivo.")]
            long idInterno,
            CancellationToken ct)
        {
            var camara = await _camaraRepository.ObtenerPorIdInternoAsync(idInterno);

            if (camara is null) return Problem(
                detail: $"No se encontró ninguna cámara con el id '{idInterno}'.",
                statusCode: StatusCodes.Status404NotFound,
                title: "Recurso no encontrado"
            );

            var resultado = await _vcaService.ObtenerModoActualAsync(camara, ct);

            if (!resultado.Exito)
                return Problem(
                    title: "Error",
                    detail: resultado.Error,
                    statusCode: StatusCodes.Status400BadRequest
                );

            return Ok(new
            {
                vca = resultado.Valor.Info().Titulo
            });
        }



        /// <summary>
        /// Cambia el modo de analítica de video (VCA) de la cámara.
        /// </summary>
        /// <remarks>
        /// Operación intrusiva: en la mayoría de las plataformas el cambio de modo exige reiniciar el dispositivo,
        /// y la plataforma lo hace automáticamente tras aplicar la configuración.
        ///
        /// Consideraciones y limitaciones:
        /// - La cámara se reinicia. Cuando la plataforma del dispositivo lo requiere, al final de la operación se
        ///   envía un reinicio vía ISAPI: la cámara deja de transmitir y de generar eventos durante uno o dos
        ///   minutos aproximadamente. La respuesta 200 confirma que la orden se aplicó, no que el dispositivo ya
        ///   esté operativo de nuevo.
        /// - El modo solicitado debe estar en la lista que devuelve <c>GET /camaras/{idInterno}/vca/modos</c>. La
        ///   plataforma valida los modos soportados antes de escribir la configuración; si el hardware no lo
        ///   soporta se responde 400 sin tocar el dispositivo.
        /// - El valor de <c>vca</c> es el identificador del modo, no su descripción. Valores admitidos:
        ///   <c>alarma_recuento_personas</c>, <c>anpr</c>, <c>armado_pista_persona</c>, <c>captura_facial</c>,
        ///   <c>deteccion_multiobjetivo</c>, <c>deteccion_multiobjetivo_comparacion</c>, <c>evento_smart</c>,
        ///   <c>monitorizacion</c>, <c>ninguno</c>, <c>recuento_personas</c> y <c>trafico_rodado</c>.
        ///   La comparación no distingue mayúsculas de minúsculas; un identificador desconocido responde 400.
        /// - Cambiar el modo cambia el tipo de eventos que la cámara enviará al webhook: los eventos del modo
        ///   anterior dejan de producirse.
        /// - El resultado final es idempotente, pero el efecto no: reenviar el mismo modo vuelve a reiniciar la
        ///   cámara. Conviene comprobar primero <c>GET /camaras/{idInterno}/vca/modo</c>.
        /// - La cámara debe existir, estar activa y tener plataforma VCA asignada.
        /// </remarks>
        /// <param name="idInterno">Identificador interno de la cámara, obtenido de <c>GET /camaras</c>. Debe ser un entero positivo.</param>
        /// <param name="vca">Identificador del modo VCA a establecer. Máximo 100 caracteres. Debe ser uno de los valores listados en <c>GET /camaras/{idInterno}/vca/modos</c>.</param>
        /// <param name="ct">Token de cancelación gestionado por el framework.</param>
        /// <response code="200">El modo se aplicó correctamente. El dispositivo puede estar reiniciándose.</response>
        /// <response code="400">El identificador de modo no es válido, la cámara no soporta ese modo o no soporta VCA, o el dispositivo no respondió correctamente.</response>
        /// <response code="404">No existe una cámara activa con el identificador indicado.</response>
        [HttpPut]
        [Route("modo/{vca}")]
        [ProducesResponseType(typeof(CambioModoResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> CambiarModo(
            [Range(1, long.MaxValue, ErrorMessage = "El parámetro '{0}' debe ser un valor positivo.")]
            long idInterno,
            [Required(ErrorMessage = "El parámetro '{0}' es obligatorio.")]
            [StringLength(100, ErrorMessage = "El parámetro '{0}' no puede superar los {1} caracteres.")]
            string vca,
            CancellationToken ct)
        {
            var camara = await _camaraRepository.ObtenerPorIdInternoAsync(idInterno);

            if (camara is null) return Problem(
                detail: $"No se encontró ninguna cámara con el id '{idInterno}'.",
                statusCode: StatusCodes.Status404NotFound,
                title: "Recurso no encontrado"
            );


            if (!VCAModoCatalogo.TryParseTitulo(vca, out var modo))
            {
                return Problem(
                    detail: $"'{vca}' no es un modo VCA válido.",
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Parámetro inválido"
                );
            }

            var resultado = await _vcaService.CambiarModoAsync(camara, modo, ct);

            if (!resultado.Exito)
                return Problem(
                    title: "Error",
                    detail: resultado.Error,
                    statusCode: StatusCodes.Status400BadRequest
                );

            return Ok(new
            {
                mensaje = $"El modo VCA ha sido actualizado correctamente a '{vca}'. El dispositivo se encuentra en proceso de reinicio y reanudará su operación en unos momentos.",
                vca = vca,
            });
        }

        /// <summary>
        /// Lista los modos VCA que soporta la cámara.
        /// </summary>
        /// <remarks>
        /// Consulta en vivo las capacidades de analítica del dispositivo. Debe usarse para poblar el selector de
        /// modos y para validar el valor antes de llamar a <c>PUT /camaras/{idInterno}/vca/modo/{vca}</c>.
        ///
        /// Consideraciones y limitaciones:
        /// - La lista depende del modelo y del firmware: dos cámaras distintas devuelven conjuntos distintos.
        /// - El campo <c>id</c> de cada elemento es el valor que acepta el endpoint de cambio de modo; el campo
        ///   <c>titulo</c> es la descripción legible para mostrar al usuario.
        /// - La cámara debe existir, estar activa y tener plataforma VCA asignada. Si está registrada sin soporte
        ///   de analítica se responde 400 con "La cámara (IP) no soporta VCA".
        /// - Si el dispositivo está inaccesible se responde 400 con el detalle del error ISAPI.
        /// - Es de solo lectura y no reinicia el dispositivo.
        /// </remarks>
        /// <param name="idInterno">Identificador interno de la cámara, obtenido de <c>GET /camaras</c>. Debe ser un entero positivo.</param>
        /// <param name="ct">Token de cancelación gestionado por el framework.</param>
        /// <response code="200">Modos VCA soportados por la cámara.</response>
        /// <response code="400">La cámara no soporta VCA o el dispositivo no respondió correctamente.</response>
        /// <response code="404">No existe una cámara activa con el identificador indicado.</response>
        [HttpGet]
        [Route("modos")]
        [ProducesResponseType(typeof(IEnumerable<ModosVCA>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> ObtenerModosSoportador(
            [Range(1, long.MaxValue, ErrorMessage = "El parámetro '{0}' debe ser un valor positivo.")]
            long idInterno,
            CancellationToken ct)
        {
            var camara = await _camaraRepository.ObtenerPorIdInternoAsync(idInterno);

            if (camara is null) return Problem(
                detail: $"No se encontró ninguna cámara con el id '{idInterno}'.",
                statusCode: StatusCodes.Status404NotFound,
                title: "Recurso no encontrado"
            );

            var resultado = await _vcaService.ObtenerModosSoportadosAsync(camara, ct);

            if (!resultado.Exito)
                return Problem(
                    title: "Error",
                    detail: resultado.Error,
                    statusCode: StatusCodes.Status400BadRequest
                );

            return Ok(resultado.Valor!.Select(modo =>
                new ModosVCA(modo.Info().Titulo, modo.Info().Descripcion)));
        }
    }
}
