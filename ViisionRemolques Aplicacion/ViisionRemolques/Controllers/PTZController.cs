using Microsoft.AspNetCore.Mvc;
using ViisionRemolques.Repositories;
using ViisionRemolques.Services.ISAPI.PTZ;
using System.ComponentModel.DataAnnotations;

namespace ViisionRemolques.Controllers
{
    /// <summary>
    /// Control PTZ (paneo, tilt, zoom), óptica (enfoque, iris) y acciones auxiliares de una cámara motorizada.
    /// </summary>
    /// <remarks>
    /// Todas las operaciones se envían en vivo al canal 1 del dispositivo vía ISAPI y requieren que la cámara
    /// declare soporte para la acción solicitada en <c>GET /camaras</c> (bloque <c>funcionalidades.ptz</c>).
    ///
    /// Modelo de movimiento continuo: los endpoints de <c>mover</c>, <c>zoom</c>, <c>enfoque</c> e <c>iris</c> no
    /// reciben una posición destino sino una velocidad. El dispositivo mantiene el movimiento hasta recibir una
    /// nueva orden con valor <c>0</c> o hasta alcanzar su tope mecánico. El cliente es responsable de enviar la
    /// orden de parada: por ejemplo, <c>PUT /camaras/1/ptz/mover/0/0</c> al soltar el control de la interfaz.
    /// </remarks>
    [ApiController]
    [Route("camaras/{idInterno:long}/ptz")]
    [Produces("application/json")]
    public class PTZController : Controller
    {
        private readonly PtzService _ptzService;
        private readonly CamaraRepository _camaraRepository;

        public PTZController(PtzService ptzService, CamaraRepository camaraRepository)
        {
            _ptzService = ptzService;
            _camaraRepository = camaraRepository;
        }

        /// <summary>
        /// Mueve la cámara de forma continua en paneo y tilt.
        /// </summary>
        /// <remarks>
        /// Los valores no son coordenadas sino velocidades: el dispositivo arranca el movimiento y lo mantiene
        /// indefinidamente.
        ///
        /// Consideraciones y limitaciones:
        /// - Para detener el movimiento hay que enviar explícitamente <c>PUT /camaras/{idInterno}/ptz/mover/0/0</c>.
        ///   Si no se envía la parada, la cámara sigue girando hasta su límite mecánico.
        /// - <c>pan</c> positivo gira a la derecha y negativo a la izquierda; <c>tilt</c> positivo sube y negativo
        ///   baja. La magnitud (1-100) es la velocidad; <c>0</c> detiene ese eje.
        /// - Requiere que la cámara declare <c>funcionalidades.ptz.movimiento = true</c>. En caso contrario se
        ///   responde 400 con "La cámara (IP) no soporta movimiento PTZ", sin contactar al dispositivo.
        /// - Opera siempre sobre el canal 1 del dispositivo.
        /// - La respuesta 200 no devuelve cuerpo: confirma que el dispositivo aceptó la orden, no que haya
        ///   terminado de moverse. No existe endpoint de consulta de posición.
        /// - Varias órdenes consecutivas se sobreescriben: la última recibida es la que queda activa.
        /// </remarks>
        /// <param name="idInterno">Identificador interno de la cámara, obtenido de <c>GET /camaras</c>. Debe ser un entero positivo.</param>
        /// <param name="pan">Velocidad horizontal entre -100 y 100. Negativo gira a la izquierda, positivo a la derecha, <c>0</c> detiene el eje.</param>
        /// <param name="tilt">Velocidad vertical entre -100 y 100. Negativo baja, positivo sube, <c>0</c> detiene el eje.</param>
        /// <param name="ct">Token de cancelación gestionado por el framework.</param>
        /// <response code="200">El dispositivo aceptó la orden de movimiento.</response>
        /// <response code="400">La cámara no soporta movimiento PTZ o el dispositivo rechazó la orden.</response>
        /// <response code="404">No existe una cámara activa con el identificador indicado.</response>
        [HttpPut]
        [Route("mover/{pan:int}/{tilt:int}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Mover(
            [Range(1, long.MaxValue, ErrorMessage = "El parámetro '{0}' debe ser un valor positivo.")]
            long idInterno,
            [Range(-100, 100, ErrorMessage = "El valor de {0} debe estar entre -100 y 100.")]
            int pan,
            [Range(-100, 100, ErrorMessage = "El valor de {0} debe estar entre -100 y 100.")]
            int tilt,
            CancellationToken ct)
        {
            var camara = await _camaraRepository.ObtenerPorIdInternoAsync(idInterno);
            if (camara is null)
                return Problem(
                    detail: $"No se encontró ninguna cámara con el id '{idInterno}'.",
                    statusCode: StatusCodes.Status404NotFound,
                    title: "Recurso no encontrado"
                );

            var resultado = await _ptzService.MoverAsync(camara, pan, tilt, ct);
            if (!resultado.Exito)
                return Problem(
                    title: "Error",
                    detail: resultado.Error,
                    statusCode: StatusCodes.Status400BadRequest
                );

            return Ok();
        }

        /// <summary>
        /// Aplica zoom óptico continuo (acercar o alejar).
        /// </summary>
        /// <remarks>
        /// Consideraciones y limitaciones:
        /// - El valor es una velocidad, no un nivel de zoom. El movimiento continúa hasta recibir
        ///   <c>PUT /camaras/{idInterno}/ptz/zoom/0</c> o hasta llegar al tope del objetivo.
        /// - Positivo acerca (tele) y negativo aleja (gran angular); <c>0</c> detiene el zoom.
        /// - Requiere <c>funcionalidades.ptz.zoom = true</c>; si no, se responde 400 con "La cámara (IP) no
        ///   soporta zoom" sin contactar al dispositivo.
        /// - Opera siempre sobre el canal 1 del dispositivo.
        /// - Tras un zoom amplio puede ser necesario reenfocar con <c>enfoque-auxiliar</c>.
        /// - No existe endpoint para consultar el nivel de zoom actual.
        /// </remarks>
        /// <param name="idInterno">Identificador interno de la cámara, obtenido de <c>GET /camaras</c>. Debe ser un entero positivo.</param>
        /// <param name="valor">Velocidad de zoom entre -100 y 100. Positivo acerca, negativo aleja, <c>0</c> detiene.</param>
        /// <param name="ct">Token de cancelación gestionado por el framework.</param>
        /// <response code="200">El dispositivo aceptó la orden de zoom.</response>
        /// <response code="400">La cámara no soporta zoom o el dispositivo rechazó la orden.</response>
        /// <response code="404">No existe una cámara activa con el identificador indicado.</response>
        [HttpPut]
        [Route("zoom/{valor:int}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Zoom(
            [Range(1, long.MaxValue, ErrorMessage = "El parámetro '{0}' debe ser un valor positivo.")]
            long idInterno,
            [Range(-100, 100, ErrorMessage = "El valor de {0} debe estar entre -100 y 100.")]
            int valor,
            CancellationToken ct)
        {
            var camara = await _camaraRepository.ObtenerPorIdInternoAsync(idInterno);
            if (camara is null)
                return Problem(
                    detail: $"No se encontró ninguna cámara con el id '{idInterno}'.",
                    statusCode: StatusCodes.Status404NotFound,
                    title: "Recurso no encontrado"
                );

            var resultado = await _ptzService.ZoomAsync(camara, valor, ct);
            if (!resultado.Exito)
                return Problem(
                    title: "Error",
                    detail: resultado.Error,
                    statusCode: StatusCodes.Status400BadRequest
                );

            return Ok();
        }

        /// <summary>
        /// Ajusta el enfoque manual del objetivo.
        /// </summary>
        /// <remarks>
        /// Consideraciones y limitaciones:
        /// - El valor es una velocidad de desplazamiento del enfoque, no una distancia. Continúa hasta recibir
        ///   <c>PUT /camaras/{idInterno}/ptz/enfoque/0</c> o hasta el tope del recorrido.
        /// - Positivo enfoca hacia lejos y negativo hacia cerca; <c>0</c> detiene el movimiento.
        /// - Requiere <c>funcionalidades.ptz.enfoque = true</c>; si no, se responde 400 con "La cámara (IP) no
        ///   soporta enfoque" sin contactar al dispositivo.
        /// - Enfocar manualmente suele desactivar el autofoco del dispositivo hasta que se ejecuta
        ///   <c>enfoque-auxiliar</c> (enfoque automático de una pulsación).
        /// - Opera siempre sobre el canal 1 del dispositivo.
        /// </remarks>
        /// <param name="idInterno">Identificador interno de la cámara, obtenido de <c>GET /camaras</c>. Debe ser un entero positivo.</param>
        /// <param name="valor">Velocidad de enfoque entre -100 y 100. Positivo enfoca lejos, negativo cerca, <c>0</c> detiene.</param>
        /// <param name="ct">Token de cancelación gestionado por el framework.</param>
        /// <response code="200">El dispositivo aceptó la orden de enfoque.</response>
        /// <response code="400">La cámara no soporta enfoque o el dispositivo rechazó la orden.</response>
        /// <response code="404">No existe una cámara activa con el identificador indicado.</response>
        [HttpPut]
        [Route("enfoque/{valor:int}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Enfocar(
            [Range(1, long.MaxValue, ErrorMessage = "El parámetro '{0}' debe ser un valor positivo.")]
            long idInterno,
            [Range(-100, 100, ErrorMessage = "El valor de {0} debe estar entre -100 y 100.")]
            int valor,
            CancellationToken ct)
        {
            var camara = await _camaraRepository.ObtenerPorIdInternoAsync(idInterno);
            if (camara is null)
                return Problem(
                    detail: $"No se encontró ninguna cámara con el id '{idInterno}'.",
                    statusCode: StatusCodes.Status404NotFound,
                    title: "Recurso no encontrado"
                );

            var resultado = await _ptzService.EnfocarAsync(camara, valor, ct);
            if (!resultado.Exito)
                return Problem(
                    title: "Error",
                    detail: resultado.Error,
                    statusCode: StatusCodes.Status400BadRequest
                );

            return Ok();
        }

        /// <summary>
        /// Ajusta la apertura del iris (exposición).
        /// </summary>
        /// <remarks>
        /// Consideraciones y limitaciones:
        /// - El valor es una velocidad de apertura o cierre, no un valor de diafragma. Continúa hasta recibir
        ///   <c>PUT /camaras/{idInterno}/ptz/iris/0</c> o hasta el tope del recorrido.
        /// - Positivo abre el iris (imagen más clara) y negativo lo cierra (imagen más oscura); <c>0</c> detiene.
        /// - Requiere <c>funcionalidades.ptz.iris = true</c>; si no, se responde 400 con "La cámara (IP) no
        ///   soporta iris" sin contactar al dispositivo.
        /// - En cámaras con exposición automática el ajuste manual puede ser revertido por el propio dispositivo.
        /// - Opera siempre sobre el canal 1 del dispositivo.
        /// </remarks>
        /// <param name="idInterno">Identificador interno de la cámara, obtenido de <c>GET /camaras</c>. Debe ser un entero positivo.</param>
        /// <param name="valor">Velocidad de ajuste del iris entre -100 y 100. Positivo abre, negativo cierra, <c>0</c> detiene.</param>
        /// <param name="ct">Token de cancelación gestionado por el framework.</param>
        /// <response code="200">El dispositivo aceptó la orden de iris.</response>
        /// <response code="400">La cámara no soporta iris o el dispositivo rechazó la orden.</response>
        /// <response code="404">No existe una cámara activa con el identificador indicado.</response>
        [HttpPut]
        [Route("iris/{valor:int}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> AjustarIris(
            [Range(1, long.MaxValue, ErrorMessage = "El parámetro '{0}' debe ser un valor positivo.")]
            long idInterno,
            [Range(-100, 100, ErrorMessage = "El valor de {0} debe estar entre -100 y 100.")]
            int valor,
            CancellationToken ct)
        {
            var camara = await _camaraRepository.ObtenerPorIdInternoAsync(idInterno);
            if (camara is null)
                return Problem(
                    detail: $"No se encontró ninguna cámara con el id '{idInterno}'.",
                    statusCode: StatusCodes.Status404NotFound,
                    title: "Recurso no encontrado"
                );

            var resultado = await _ptzService.AjustarIrisAsync(camara, valor, ct);
            if (!resultado.Exito)
                return Problem(
                    title: "Error",
                    detail: resultado.Error,
                    statusCode: StatusCodes.Status400BadRequest
                );

            return Ok();
        }

        /// <summary>
        /// Acciona una pasada del limpiaparabrisas de la cámara.
        /// </summary>
        /// <remarks>
        /// Consideraciones y limitaciones:
        /// - Es una acción puntual (no continua): el dispositivo ejecuta el ciclo de limpieza y se detiene solo.
        ///   No existe orden de parada.
        /// - No recibe parámetros adicionales ni cuerpo de petición.
        /// - Requiere <c>funcionalidades.ptz.escobilla = true</c>; si no, se responde 400 con "La cámara (IP) no
        ///   soporta escobilla" sin contactar al dispositivo.
        /// - Solo aplica a domos de exterior con limpiaparabrisas físico. Es un elemento mecánico con desgaste:
        ///   conviene evitar activaciones repetidas innecesarias.
        /// - Opera siempre sobre el canal 1 del dispositivo.
        /// </remarks>
        /// <param name="idInterno">Identificador interno de la cámara, obtenido de <c>GET /camaras</c>. Debe ser un entero positivo.</param>
        /// <param name="ct">Token de cancelación gestionado por el framework.</param>
        /// <response code="200">El dispositivo aceptó la orden y ejecuta el ciclo de limpieza.</response>
        /// <response code="400">La cámara no soporta escobilla o el dispositivo rechazó la orden.</response>
        /// <response code="404">No existe una cámara activa con el identificador indicado.</response>
        [HttpPut]
        [Route("escobilla")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Escobilla(
            [Range(1, long.MaxValue, ErrorMessage = "El parámetro '{0}' debe ser un valor positivo.")]
            long idInterno,
            CancellationToken ct)
        {
            var camara = await _camaraRepository.ObtenerPorIdInternoAsync(idInterno);
            if (camara is null)
                return Problem(
                    detail: $"No se encontró ninguna cámara con el id '{idInterno}'.",
                    statusCode: StatusCodes.Status404NotFound,
                    title: "Recurso no encontrado"
                );

            var resultado = await _ptzService.EscobillaAsync(camara, ct);
            if (!resultado.Exito)
                return Problem(
                    title: "Error",
                    detail: resultado.Error,
                    statusCode: StatusCodes.Status400BadRequest
                );

            return Ok();
        }

        /// <summary>
        /// Ejecuta un enfoque automático de una pulsación (one push focus).
        /// </summary>
        /// <remarks>
        /// Consideraciones y limitaciones:
        /// - Es una acción puntual: el dispositivo busca el mejor enfoque para la escena actual y se detiene solo.
        ///   No existe orden de parada.
        /// - Es la forma de recuperar el autofoco después de haber usado <c>PUT .../ptz/enfoque/{valor}</c>.
        /// - No recibe parámetros adicionales ni cuerpo de petición.
        /// - Requiere <c>funcionalidades.ptz.enfoqueAuxiliar = true</c>; si no, se responde 400 con "La cámara (IP)
        ///   no soporta enfoque auxiliar" sin contactar al dispositivo.
        /// - La respuesta 200 confirma que la orden fue aceptada; el ajuste puede tardar un par de segundos en
        ///   completarse y el resultado no se reporta.
        /// - Opera siempre sobre el canal 1 del dispositivo.
        /// </remarks>
        /// <param name="idInterno">Identificador interno de la cámara, obtenido de <c>GET /camaras</c>. Debe ser un entero positivo.</param>
        /// <param name="ct">Token de cancelación gestionado por el framework.</param>
        /// <response code="200">El dispositivo aceptó la orden de enfoque automático.</response>
        /// <response code="400">La cámara no soporta enfoque auxiliar o el dispositivo rechazó la orden.</response>
        /// <response code="404">No existe una cámara activa con el identificador indicado.</response>
        [HttpPut]
        [Route("enfoque-auxiliar")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> EnfoqueAuxiliar(
            [Range(1, long.MaxValue, ErrorMessage = "El parámetro '{0}' debe ser un valor positivo.")]
            long idInterno,
            CancellationToken ct)
        {
            var camara = await _camaraRepository.ObtenerPorIdInternoAsync(idInterno);
            if (camara is null)
                return Problem(
                    detail: $"No se encontró ninguna cámara con el id '{idInterno}'.",
                    statusCode: StatusCodes.Status404NotFound,
                    title: "Recurso no encontrado"
                );

            var resultado = await _ptzService.EnfoqueAuxiliarAsync(camara, ct);
            if (!resultado.Exito)
                return Problem(
                    title: "Error",
                    detail: resultado.Error,
                    statusCode: StatusCodes.Status400BadRequest
                );

            return Ok();
        }

        /// <summary>
        /// Reinicia el objetivo a su posición de referencia (reset del enfoque).
        /// </summary>
        /// <remarks>
        /// Operación de mantenimiento: devuelve el bloque óptico a su punto de referencia de fábrica. Se usa
        /// cuando el enfoque queda "perdido" y el enfoque automático ya no logra una imagen nítida.
        ///
        /// Consideraciones y limitaciones:
        /// - Es una acción puntual y no se puede interrumpir: durante el proceso la imagen se desenfoca y se
        ///   recorre el objetivo, lo que puede tomar varios segundos.
        /// - Descarta cualquier ajuste manual de enfoque previo.
        /// - No recibe parámetros adicionales ni cuerpo de petición.
        /// - Requiere <c>funcionalidades.ptz.inicializacionObjetivo = true</c>; si no, se responde 400 con
        ///   "La cámara (IP) no soporta inicialización del objetivo" sin contactar al dispositivo.
        /// - No debe ejecutarse de forma rutinaria: implica recorrido mecánico del objetivo.
        /// - Opera siempre sobre el canal 1 del dispositivo.
        /// </remarks>
        /// <param name="idInterno">Identificador interno de la cámara, obtenido de <c>GET /camaras</c>. Debe ser un entero positivo.</param>
        /// <param name="ct">Token de cancelación gestionado por el framework.</param>
        /// <response code="200">El dispositivo aceptó la orden de inicialización del objetivo.</response>
        /// <response code="400">La cámara no soporta esta acción o el dispositivo rechazó la orden.</response>
        /// <response code="404">No existe una cámara activa con el identificador indicado.</response>
        [HttpPut]
        [Route("inicializacion-objetivo")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> InicializarObjetivo(
            [Range(1, long.MaxValue, ErrorMessage = "El parámetro '{0}' debe ser un valor positivo.")]
            long idInterno,
            CancellationToken ct)
        {
            var camara = await _camaraRepository.ObtenerPorIdInternoAsync(idInterno);
            if (camara is null)
                return Problem(
                    detail: $"No se encontró ninguna cámara con el id '{idInterno}'.",
                    statusCode: StatusCodes.Status404NotFound,
                    title: "Recurso no encontrado"
                );

            var resultado = await _ptzService.InicializarObjetivoAsync(camara, ct);
            if (!resultado.Exito)
                return Problem(
                    title: "Error",
                    detail: resultado.Error,
                    statusCode: StatusCodes.Status400BadRequest
                );

            return Ok();
        }

        /// <summary>
        /// Recalibra el recorrido del zoom del objetivo.
        /// </summary>
        /// <remarks>
        /// Operación de mantenimiento: el dispositivo vuelve a medir el recorrido del zoom para corregir la
        /// deriva entre el zoom solicitado y el real.
        ///
        /// Consideraciones y limitaciones:
        /// - Es una acción puntual y no se puede interrumpir: durante la calibración el objetivo recorre todo su
        ///   rango y la imagen queda inutilizable unos segundos.
        /// - No recibe parámetros adicionales ni cuerpo de petición (el tipo de calibración se fija internamente
        ///   en <c>zoom</c>).
        /// - Requiere <c>funcionalidades.ptz.calibracionZoom = true</c>; si no, se responde 400 con "La cámara (IP)
        ///   no soporta calibración de zoom" sin contactar al dispositivo.
        /// - No debe ejecutarse de forma rutinaria: implica recorrido mecánico completo del objetivo.
        /// - Opera siempre sobre el canal 1 del dispositivo.
        /// </remarks>
        /// <param name="idInterno">Identificador interno de la cámara, obtenido de <c>GET /camaras</c>. Debe ser un entero positivo.</param>
        /// <param name="ct">Token de cancelación gestionado por el framework.</param>
        /// <response code="200">El dispositivo aceptó la orden de calibración.</response>
        /// <response code="400">La cámara no soporta calibración de zoom o el dispositivo rechazó la orden.</response>
        /// <response code="404">No existe una cámara activa con el identificador indicado.</response>
        [HttpPut]
        [Route("calibracion-zoom")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> CalibrarZoom(
            [Range(1, long.MaxValue, ErrorMessage = "El parámetro '{0}' debe ser un valor positivo.")]
            long idInterno,
            CancellationToken ct)
        {
            var camara = await _camaraRepository.ObtenerPorIdInternoAsync(idInterno);
            if (camara is null)
                return Problem(
                    detail: $"No se encontró ninguna cámara con el id '{idInterno}'.",
                    statusCode: StatusCodes.Status404NotFound,
                    title: "Recurso no encontrado"
                );

            var resultado = await _ptzService.CalibrarZoomAsync(camara, ct);
            if (!resultado.Exito)
                return Problem(
                    title: "Error",
                    detail: resultado.Error,
                    statusCode: StatusCodes.Status400BadRequest
                );

            return Ok();
        }
    }

}
