using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text.Json.Nodes;
using System.Web;
using ViisionRemolques.Services.ISAPI;

namespace ViisionRemolques.Controllers
{
    /// <summary>
    /// Búsqueda de video por descripción en lenguaje natural (Acuseek) sobre las grabaciones del MVR.
    /// </summary>
    /// <remarks>
    /// Las búsquedas son asíncronas y se ejecutan en el propio MVR: primero se inicia la búsqueda
    /// (<c>POST /acuseek/busqueda</c>), después se consulta su avance y resultados
    /// (<c>POST /acuseek/busqueda/estado</c>) y al terminar conviene liberarla
    /// (<c>DELETE /acuseek/busqueda/{taskId}</c>).
    ///
    /// Todos los endpoints de este grupo operan contra el MVR configurado en el servidor (un único equipo), no
    /// contra cámaras individuales. Cuando el MVR no responde o devuelve un error, la API responde 502.
    /// </remarks>
    [ApiController]
    [Route("/acuseek")]
    public class AcuseekController : ControllerBase
    {
        private readonly AcuseekService _acuseekService;

        public AcuseekController(AcuseekService acuseekService)
        {
            _acuseekService = acuseekService;
        }

        /// <summary>
        /// Obtiene la fecha y hora local del MVR junto con su desplazamiento horario.
        /// </summary>
        /// <remarks>
        /// El MVR trabaja en su propia hora local y es la referencia que deben usar los rangos de búsqueda y las
        /// fechas de clips. Este endpoint permite alinear el reloj del cliente con el del equipo.
        ///
        /// Consideraciones y limitaciones:
        /// - Devuelve <c>hora</c> (fecha y hora local del equipo, sin zona) y <c>offset</c> (desplazamiento con
        ///   respecto a UTC, p. ej. <c>-06:00</c>). Para construir una fecha ISO 8601 completa hay que concatenar
        ///   ambos valores.
        /// - La hora del MVR puede no coincidir con la del servidor de la API ni con la del navegador. Al usar el
        ///   rango <c>custom</c> en las búsquedas, las fechas se interpretan en la hora local del MVR.
        /// - Es de solo lectura y no recibe parámetros.
        /// - Si el equipo está inaccesible o devuelve una hora no interpretable, se responde 502.
        /// </remarks>
        /// <param name="ct">Token de cancelación gestionado por el framework.</param>
        /// <response code="200">Hora local y desplazamiento horario del MVR.</response>
        /// <response code="502">No fue posible comunicarse con el MVR o la hora devuelta no es válida.</response>
        [HttpGet]
        [Route("hora")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status502BadGateway)]
        public async Task<IActionResult> ObtenerHora(CancellationToken ct)
        {
            var resultado = await _acuseekService.ObtenerHoraDispositivoAsync(ct);
            if (!resultado.Exito)
                return ErrorMvr(resultado.Error);

            return Ok(new { hora = resultado.Valor.Hora, offset = resultado.Valor.Offset });
        }

        /// <summary>
        /// Lista los canales de video dados de alta en el MVR.
        /// </summary>
        /// <remarks>
        /// Devuelve los canales que el MVR tiene configurados, con su identificador y nombre. El identificador es
        /// el valor que se usa en el campo <c>Canales</c> de una búsqueda y en el campo <c>Canal</c> al solicitar
        /// un clip (<c>POST /video/token/clip</c>).
        ///
        /// Consideraciones y limitaciones:
        /// - Refleja la configuración del MVR, no el estado de las cámaras: un canal puede estar listado y no
        ///   tener señal en este momento.
        /// - Los canales sin nombre configurado se devuelven como <c>Canal {id}</c>.
        /// - No está paginado y no recibe parámetros.
        /// - Si el equipo está inaccesible se responde 502.
        /// </remarks>
        /// <param name="ct">Token de cancelación gestionado por el framework.</param>
        /// <response code="200">Canales configurados en el MVR. Puede ser una colección vacía.</response>
        /// <response code="502">No fue posible comunicarse con el MVR.</response>
        [HttpGet]
        [Route("canales")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status502BadGateway)]
        public async Task<IActionResult> ObtenerCanales(CancellationToken ct)
        {
            var resultado = await _acuseekService.ObtenerCanalesAsync(ct);
            if (!resultado.Exito)
                return ErrorMvr(resultado.Error);

            return Ok(resultado.Valor!.Select(c => new { id = c.Id, nombre = c.Nombre }));
        }

        /// <summary>
        /// Obtiene el historial de términos buscados en el MVR.
        /// </summary>
        /// <remarks>
        /// Devuelve las últimas búsquedas registradas por el propio equipo, pensado para ofrecer sugerencias
        /// rápidas en la interfaz.
        ///
        /// Consideraciones y limitaciones:
        /// - El historial lo mantiene el MVR, es global al equipo y no está asociado a un usuario de la API.
        /// - El tamaño y el criterio de rotación los decide el dispositivo; no se puede paginar, filtrar ni
        ///   borrar desde esta API.
        /// - Incluye los términos buscados desde cualquier cliente del MVR, no solo desde esta plataforma.
        /// - Devuelve una lista de textos planos; puede estar vacía si nunca se ha buscado.
        /// - Si el equipo está inaccesible se responde 502.
        /// </remarks>
        /// <param name="ct">Token de cancelación gestionado por el framework.</param>
        /// <response code="200">Lista de términos buscados previamente.</response>
        /// <response code="502">No fue posible comunicarse con el MVR.</response>
        [HttpGet]
        [Route("historial")]
        [ProducesResponseType(typeof(IEnumerable<string>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status502BadGateway)]
        public async Task<IActionResult> ObtenerHistorial(CancellationToken ct)
        {
            var resultado = await _acuseekService.ObtenerHistorialAsync(ct);
            if (!resultado.Exito)
                return ErrorMvr(resultado.Error);

            return Ok(resultado.Valor);
        }

        /// <summary>
        /// Obtiene los términos de búsqueda marcados como favoritos en el MVR.
        /// </summary>
        /// <remarks>
        /// Consideraciones y limitaciones:
        /// - Los favoritos los almacena el MVR y son globales al equipo: no están asociados a un usuario de la API.
        /// - Se gestionan con <c>POST /acuseek/favoritos</c> (alta y baja).
        /// - Devuelve una lista de textos planos; puede estar vacía.
        /// - No está paginado y no recibe parámetros.
        /// - Si el equipo está inaccesible se responde 502.
        /// </remarks>
        /// <param name="ct">Token de cancelación gestionado por el framework.</param>
        /// <response code="200">Lista de términos favoritos.</response>
        /// <response code="502">No fue posible comunicarse con el MVR.</response>
        [HttpGet]
        [Route("favoritos")]
        [ProducesResponseType(typeof(IEnumerable<string>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status502BadGateway)]
        public async Task<IActionResult> ObtenerFavoritos(CancellationToken ct)
        {
            var resultado = await _acuseekService.ObtenerFavoritosAsync(ct);
            if (!resultado.Exito)
                return ErrorMvr(resultado.Error);

            return Ok(resultado.Valor);
        }

        /// <summary>
        /// Representa la solicitud para agregar o quitar un término de la lista de favoritos.
        /// </summary>
        /// <param name="Termino">Término de búsqueda a marcar o desmarcar. Obligatorio, máximo 100 caracteres.</param>
        /// <param name="Agregar">
        /// <c>true</c> (valor por omisión) agrega el término a favoritos; <c>false</c> lo quita.
        /// </param>
        public record FavoritoRequest(
            [Required(ErrorMessage = "El parametro '{0}' es obligatorio")]
            [MaxLength(100)]
            string Termino,
            bool Agregar = true
        );

        /// <summary>
        /// Agrega o quita un término de la lista de favoritos del MVR.
        /// </summary>
        /// <remarks>
        /// Un mismo endpoint cubre el alta y la baja según el valor de <c>Agregar</c>.
        ///
        /// Consideraciones y limitaciones:
        /// - La lista de favoritos es global al MVR: el cambio afecta a todos los clientes del equipo.
        /// - La operación es idempotente: agregar un término ya existente o quitar uno que no está no produce error.
        /// - El término debe coincidir con el texto que se quiere guardar; no se normaliza ni se recorta en esta API.
        /// - La respuesta 200 no devuelve cuerpo. Para ver el resultado hay que consultar
        ///   <c>GET /acuseek/favoritos</c>.
        /// - El MVR puede rechazar la operación (por ejemplo, al alcanzar su límite de favoritos); en ese caso se
        ///   responde 502 con el mensaje devuelto por el equipo.
        /// </remarks>
        /// <param name="request">Término a marcar o desmarcar y la acción a realizar.</param>
        /// <param name="ct">Token de cancelación gestionado por el framework.</param>
        /// <response code="200">El favorito se actualizó correctamente.</response>
        /// <response code="400">Falta el término o excede los 100 caracteres.</response>
        /// <response code="502">El MVR rechazó la operación o no fue posible comunicarse con él.</response>
        [HttpPost]
        [Route("favoritos")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status502BadGateway)]
        public async Task<IActionResult> EstablecerFavorito([FromBody] FavoritoRequest request, CancellationToken ct)
        {
            var resultado = await _acuseekService.EstablecerFavoritoAsync(request.Termino, request.Agregar, ct);
            if (!resultado.Exito)
                return ErrorMvr(resultado.Error);

            return Ok();
        }

        /// <summary>
        /// Representa la solicitud de búsqueda de clips con filtros de tiempo y similitud.
        /// </summary>
        /// <param name="Texto">Texto, palabras clave o descripción a buscar en Acuseek.</param>
        /// <param name="Rango">Rango de tiempo predefinido o personalizado. Los valores permitidos son 'today', '3d', '7d' y 'custom'</param>
        /// <param name="Start">Fecha de inicio en formato ISO 8601 (Requerido solo si Rango es 'custom').</param>
        /// <param name="End">Fecha de fin en formato ISO 8601 (Requerido solo si Rango es 'custom').</param>
        /// <param name="Canales">Lista de canales a consultar. Si se omite o está vacío (se recomienda siempre omitir), se buscará en todos (1-16).</param>
        /// <param name="Similarity">Porcentaje mínimo de similitud requerido para considerar una coincidencia válida.</param>
        public record BusquedaRequest(
            [Required(ErrorMessage = "El parametro '{0}' es obligatorio")]
            string Texto,
            [Required(ErrorMessage = "El parametro '{0}' es obligatorio")]
            [AllowedValues("today", "3d", "7d", "custom", ErrorMessage = "Los valores validos para 'Rango' son 'today', '3d', '7d' o 'custom'")]
            string Rango,
            string? Start,
            string? End,
            int[]? Canales,
            [Range(1, 100, ErrorMessage = "La similitud debe ser un valor entre 1 y 100.")]
            int Similarity
        );

        /// <summary>
        /// Inicia una búsqueda de video por descripción en lenguaje natural.
        /// </summary>
        /// <remarks>
        /// La búsqueda es asíncrona: este endpoint solo la encola en el MVR y devuelve un <c>taskId</c>. Los
        /// resultados se obtienen consultando <c>POST /acuseek/busqueda/estado</c> con ese identificador, y al
        /// terminar la tarea debe liberarse con <c>DELETE /acuseek/busqueda/{taskId}</c>.
        ///
        /// Consideraciones y limitaciones:
        /// - El rango de tiempo se calcula en la hora local del MVR, no en la del cliente:
        ///   <c>today</c> es el día en curso, <c>3d</c> los últimos 3 días y <c>7d</c> los últimos 7, siempre
        ///   completos hasta el final del día de hoy.
        /// - Con <c>custom</c>, <c>Start</c> y <c>End</c> deben ser fechas ISO 8601. Si falta alguna o no se puede
        ///   interpretar, se sustituye silenciosamente por el inicio o el fin del día de hoy, sin devolver error.
        /// - El rango efectivo está limitado por la retención de grabación del MVR: pedir fechas más antiguas que
        ///   la grabación disponible devuelve una búsqueda sin coincidencias.
        /// - Si <c>Canales</c> se omite o va vacío, se buscan los canales 1 al 16. Se recomienda omitirlo; para
        ///   restringir la búsqueda deben usarse los identificadores de <c>GET /acuseek/canales</c>.
        /// - El texto se envía al motor con idioma fijo español y se recorta de espacios al inicio y al final.
        /// - <c>Similarity</c> es el umbral mínimo de coincidencia (1-100). Valores altos devuelven menos
        ///   resultados pero más precisos; el mismo umbral puede volverse a aplicar al consultar el estado.
        /// - Cada llamada crea una tarea nueva en el MVR y consume recursos del equipo: el número de búsquedas
        ///   simultáneas que admite es limitado, por lo que no deben lanzarse en bucle sin liberar las anteriores.
        /// - La respuesta 200 solo confirma que la tarea se creó; no implica que existan coincidencias.
        /// - Internamente se consulta primero la hora del MVR, por lo que un fallo de comunicación con el equipo
        ///   también responde 502.
        /// </remarks>
        /// <param name="request">Texto, rango de tiempo, canales y umbral de similitud de la búsqueda.</param>
        /// <param name="ct">Token de cancelación gestionado por el framework.</param>
        /// <response code="200">Búsqueda encolada. Devuelve el <c>taskId</c> con el que se consulta su estado.</response>
        /// <response code="400">Falta el texto o el rango, el rango no es uno de los valores admitidos, o la similitud está fuera de 1-100.</response>
        /// <response code="502">No fue posible comunicarse con el MVR o el equipo no devolvió un identificador de tarea.</response>
        [HttpPost]
        [Route("busqueda")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status502BadGateway)]
        public async Task<IActionResult> IniciarBusqueda([FromBody] BusquedaRequest request, CancellationToken ct)
        {
            var horaResultado = await _acuseekService.ObtenerHoraDispositivoAsync(ct);
            if (!horaResultado.Exito)
                return ErrorMvr(horaResultado.Error);

            var (startTime, endTime) = ConstruirRango(
                request.Rango, request.Start, request.End, horaResultado.Valor.Hora, horaResultado.Valor.Offset);

            int[] canales = request.Canales is { Length: > 0 }
                ? request.Canales
                : Enumerable.Range(1, 16).ToArray();

            var resultado = await _acuseekService.IniciarBusquedaTextoAsync(request.Texto, startTime, endTime, canales, request.Similarity, ct);

            if (!resultado.Exito)
                return ErrorMvr(resultado.Error);

            return Ok(new { taskId = resultado.Valor });
        }

        /// <summary>
        /// Representa la consulta del estado y los resultados de una búsqueda en curso.
        /// </summary>
        /// <param name="TaskId">Identificador de la tarea devuelto por <c>POST /acuseek/busqueda</c>. Obligatorio.</param>
        /// <param name="SearchResultPosition">Posición del primer resultado a devolver, empezando en 1. Sirve para paginar.</param>
        /// <param name="MaxResults">Cantidad máxima de resultados a devolver en esta página.</param>
        /// <param name="Similarity">Umbral mínimo de similitud (1-100) aplicado al filtrar los resultados ya calculados.</param>
        public record EstadoBusquedaRequest(
            [Required(ErrorMessage = "El parametro '{0}' es obligatorio")]
            string TaskId,
            [Range(1, int.MaxValue, ErrorMessage = "El valor debe ser un número positivo.")]
            int SearchResultPosition,
            [Range(1, int.MaxValue, ErrorMessage = "El valor debe ser un número positivo.")]
            int MaxResults,
            [Range(1, 100, ErrorMessage = "La similitud debe ser un valor entre 1 y 100.")]
            int Similarity
        );

        /// <summary>
        /// Consulta el avance y los resultados de una búsqueda previamente iniciada.
        /// </summary>
        /// <remarks>
        /// Endpoint de sondeo: debe llamarse repetidamente con el <c>taskId</c> hasta que el campo <c>runStatus</c>
        /// indique que la búsqueda terminó. El campo <c>progress</c> permite mostrar el avance, y los resultados
        /// parciales ya disponibles se devuelven en cada llamada.
        ///
        /// Consideraciones y limitaciones:
        /// - La paginación es manual: <c>SearchResultPosition</c> es la posición del primer resultado (empieza en 1)
        ///   y <c>MaxResults</c> el tamaño de página. <c>totalMatches</c> indica cuántos resultados hay en total y
        ///   <c>numOfMatches</c> cuántos trae la página actual.
        /// - Mientras la búsqueda avanza, <c>totalMatches</c> puede crecer entre llamadas y los resultados de una
        ///   misma posición pueden cambiar.
        /// - <c>Similarity</c> vuelve a filtrar los resultados ya calculados; subirlo no relanza la búsqueda.
        /// - Las imágenes no se devuelven como URL del MVR sino como rutas de esta API hacia
        ///   <c>GET /acuseek/imagen</c>: <c>imagenCompleta</c> es el fotograma completo e <c>imagenRecorte</c> el
        ///   recorte del objeto detectado. Pueden venir nulas si el equipo no devolvió una ruta utilizable.
        /// - <c>targetRect</c> contiene la posición del objeto detectado en coordenadas relativas (0 a 1) sobre el
        ///   fotograma, útil para dibujar el recuadro sobre la imagen completa.
        /// - La tarea no es eterna: el MVR puede descartarla pasado un tiempo o al reiniciarse. Un <c>taskId</c>
        ///   desconocido o ya eliminado provoca una respuesta de error del equipo (502).
        /// - El sondeo debe espaciarse (del orden de uno o dos segundos entre llamadas) para no saturar el MVR.
        /// - Cuando la búsqueda ya no se necesita, debe liberarse con <c>DELETE /acuseek/busqueda/{taskId}</c>.
        /// </remarks>
        /// <param name="request">Identificador de la tarea, paginación y umbral de similitud.</param>
        /// <param name="ct">Token de cancelación gestionado por el framework.</param>
        /// <response code="200">Estado de la búsqueda y resultados disponibles en la página solicitada.</response>
        /// <response code="400">Falta el <c>TaskId</c>, la paginación no es positiva o la similitud está fuera de 1-100.</response>
        /// <response code="502">No fue posible comunicarse con el MVR o la tarea no existe en el equipo.</response>
        [HttpPost]
        [Route("busqueda/estado")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status502BadGateway)]
        public async Task<IActionResult> ObtenerEstadoBusqueda([FromBody] EstadoBusquedaRequest request, CancellationToken ct)
        {
            var resultado = await _acuseekService.ObtenerEstadoBusquedaAsync(request.TaskId, request.SearchResultPosition, request.MaxResults, request.Similarity, ct);

            if (!resultado.Exito)
                return ErrorMvr(resultado.Error);

            var nodoRaiz = JsonNode.Parse(resultado.Valor ?? "");

            string? ParsearPath(string? path)
            {
                if (string.IsNullOrWhiteSpace(path)) return null;

                bool esValida = Uri.TryCreate(path, UriKind.Absolute, out Uri? uriResult)
                                && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);

                if (!esValida || uriResult == null) return null;

                var queryParams = HttpUtility.ParseQueryString(uriResult.Query);
                string? name = queryParams["name"];
                string? size = queryParams["size"];

                var segmentos = uriResult.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
                var track = segmentos.LastOrDefault();

                return Url.Action(
                    action: nameof(ObtenerImagen),
                    values: new
                    {
                        track,
                        name,
                        size
                    }
                );
            }

            return Ok(new
            {
                TaskID = (string?)nodoRaiz?["taskID"],
                RunStatus = (string?)nodoRaiz?["runStatus"],
                NumOfMatches = (int?)nodoRaiz?["numOfMatches"],
                TotalMatches = (int?)nodoRaiz?["totalMatches"],
                StartTime = (string?)nodoRaiz?["startTime"],
                EndTime = (string?)nodoRaiz?["endTime"],
                Progress = (int?)nodoRaiz?["progress"],

                Resultados = nodoRaiz?["matchResults"] != null
                    ? nodoRaiz["matchResults"]!.AsArray().Select(m => new
                    {
                        Id = (int?)m?["id"],
                        Fecha = (string?)m?["time"],
                        ChannelID = (int?)m?["channelID"],
                        TargetType = (string?)m?["targetType"],
                        Similitud = (int?)m?["similarity"],
                        ImageUUID = (string?)m?["imageUUID"],

                        ImagenCompleta = ParsearPath((string?)m?["image"]?["filePath"]),

                        ImagenRecorte = ParsearPath((string?)m?["targetImage"]?["filePath"]),

                        TargetID = (int?)m?["targetID"],

                        TargetRect = m?["targetRect"] != null
                            ? new
                            {
                                Height = (double?)m["targetRect"]?["height"],
                                Width = (double?)m["targetRect"]?["width"],
                                X = (double?)m["targetRect"]?["x"],
                                Y = (double?)m["targetRect"]?["y"]
                            }
                            : null
                    }).ToList()
                    : null
            });
        }

        /// <summary>
        /// Elimina una tarea de búsqueda del MVR y libera sus recursos.
        /// </summary>
        /// <remarks>
        /// Debe llamarse siempre que una búsqueda deje de necesitarse (al cerrar la vista de resultados, al iniciar
        /// una búsqueda nueva o al cancelar una en curso).
        ///
        /// Consideraciones y limitaciones:
        /// - Tras eliminar la tarea, su <c>taskId</c> deja de ser válido: consultar su estado devolverá error.
        /// - Los resultados ya obtenidos se pierden; las imágenes referenciadas también dejan de estar disponibles.
        /// - El MVR admite un número limitado de tareas simultáneas: no liberarlas puede impedir iniciar nuevas
        ///   búsquedas.
        /// - La respuesta 200 no devuelve cuerpo.
        /// - Eliminar una tarea inexistente o ya eliminada se traduce en un error del equipo (502), por lo que la
        ///   operación no es del todo idempotente.
        /// </remarks>
        /// <param name="taskId">Identificador de la tarea devuelto por <c>POST /acuseek/busqueda</c>. Obligatorio.</param>
        /// <param name="ct">Token de cancelación gestionado por el framework.</param>
        /// <response code="200">La tarea se eliminó correctamente.</response>
        /// <response code="400">No se indicó el identificador de la tarea.</response>
        /// <response code="502">El MVR rechazó la operación o no fue posible comunicarse con él.</response>
        [HttpDelete]
        [Route("busqueda/{taskId}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status502BadGateway)]
        public async Task<IActionResult> EliminarBusqueda(
            [Required(ErrorMessage = "El parametro '{0}' es obligatorio")]
            string taskId,
            CancellationToken ct)
        {
            var resultado = await _acuseekService.EliminarBusquedaAsync(taskId, ct);
            if (!resultado.Exito)
                return ErrorMvr(resultado.Error);

            return Ok();
        }

        /// <summary>
        /// Representa el envío de un ajuste fino (tuning) del modelo para un texto de búsqueda.
        /// </summary>
        /// <param name="Texto">Texto de búsqueda sobre el que se quiere afinar el modelo. Obligatorio.</param>
        /// <param name="Positivas">
        /// Identificadores de imagen (campo <c>imageUUID</c> de los resultados de búsqueda) que SÍ corresponden al
        /// texto. Se envían al MVR como <c>positiveImageList</c>. Obligatorio, mínimo 4 elementos.
        /// </param>
        /// <param name="Negativas">
        /// Identificadores de imagen (campo <c>imageUUID</c> de los resultados de búsqueda) que NO corresponden al
        /// texto. Se envían al MVR como <c>negativeImageList</c>. Obligatorio, mínimo 6 elementos.
        /// </param>
        public record TuningSubmitRequest(
            [Required(ErrorMessage = "El texto es obligatorio.")]
            string Texto,

            [Required(ErrorMessage = "La lista de palabras positivas es obligatoria.")]
            [MinLength(4, ErrorMessage = "Se requieren como mínimo 4 palabras positivas.")]
            string[] Positivas,

            [Required(ErrorMessage = "La lista de palabras negativas es obligatoria.")]
            [MinLength(6, ErrorMessage = "Se requieren como mínimo 6 palabras negativas.")]
            string[] Negativas
        );

        /// <summary>
        /// Envía un ajuste fino (tuning) del modelo de búsqueda para un texto concreto.
        /// </summary>
        /// <remarks>
        /// Permite enseñar al motor del MVR qué resultados son correctos y cuáles no para una descripción dada, de
        /// forma que las búsquedas posteriores con ese texto sean más precisas.
        ///
        /// Consideraciones y limitaciones:
        /// - El MVR exige un mínimo de ejemplos: al menos 4 positivos y 6 negativos. Enviar menos responde 400 sin
        ///   llegar al equipo.
        /// - El texto se envía con idioma fijo español, igual que en las búsquedas; el ajuste queda asociado a ese
        ///   texto, no a la búsqueda que lo originó.
        /// - El procesamiento es asíncrono: la respuesta devuelve un <c>resultId</c> y el avance se consulta en
        ///   <c>GET /acuseek/tuning/estado</c> indicando el mismo texto.
        /// - El ajuste es global al MVR y afecta a las búsquedas de todos los clientes del equipo. No existe
        ///   endpoint para revertirlo desde esta API.
        /// - No afecta a búsquedas ya iniciadas ni a resultados ya obtenidos.
        /// - Si el equipo rechaza el envío o está inaccesible se responde 502.
        /// </remarks>
        /// <param name="request">Texto a afinar y las listas de ejemplos positivos y negativos.</param>
        /// <param name="ct">Token de cancelación gestionado por el framework.</param>
        /// <response code="200">Ajuste encolado en el MVR. Devuelve el <c>resultId</c> asignado.</response>
        /// <response code="400">Falta el texto, faltan las listas o no alcanzan el mínimo de 4 positivos y 6 negativos.</response>
        /// <response code="502">No fue posible comunicarse con el MVR o el equipo rechazó el ajuste.</response>
        [HttpPost]
        [Route("tuning")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status502BadGateway)]
        public async Task<IActionResult> EnviarTuning([FromBody] TuningSubmitRequest request, CancellationToken ct)
        {
            var resultado = await _acuseekService.EnviarTuningAsync(request.Texto, request.Positivas, request.Negativas, ct);

            if (!resultado.Exito)
                return ErrorMvr(resultado.Error);

            return Ok(new { resultId = resultado.Valor });
        }

        /// <summary>
        /// Consulta el estado del ajuste fino (tuning) asociado a un texto.
        /// </summary>
        /// <remarks>
        /// Permite saber si el ajuste enviado con <c>POST /acuseek/tuning</c> ya fue procesado por el MVR.
        ///
        /// Consideraciones y limitaciones:
        /// - La consulta se hace por texto, no por el <c>resultId</c> devuelto al enviar el ajuste: debe indicarse
        ///   exactamente el mismo texto que se usó al enviarlo.
        /// - La respuesta es el JSON del MVR tal cual, sin transformar ni normalizar: su estructura depende del
        ///   firmware del equipo y puede cambiar entre versiones. Por eso no se documenta un esquema fijo.
        /// - Solo se consulta el primer registro coincidente del equipo.
        /// - Si nunca se envió un ajuste para ese texto, el equipo responde sin registros en lugar de un error.
        /// - Si el equipo está inaccesible se responde 502.
        /// </remarks>
        /// <param name="texto">Texto de búsqueda cuyo ajuste se quiere consultar. Obligatorio y debe coincidir con el enviado en el tuning.</param>
        /// <param name="ct">Token de cancelación gestionado por el framework.</param>
        /// <response code="200">Respuesta del MVR en JSON, sin esquema fijo.</response>
        /// <response code="400">No se indicó el texto.</response>
        /// <response code="502">No fue posible comunicarse con el MVR.</response>
        [HttpGet]
        [Route("tuning/estado")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status502BadGateway)]
        public async Task<IActionResult> ObtenerEstadoTuning(
            [Required(ErrorMessage = "El parametro '{0}' es obligatorio")]
            string texto,
            CancellationToken ct)
        {
            var resultado = await _acuseekService.ObtenerEstadoTuningAsync(texto, ct);
            if (!resultado.Exito)
                return ErrorMvr(resultado.Error);

            return Content(resultado.Valor!, "application/json");
        }

        /// <summary>
        /// Descarga una imagen de un resultado de búsqueda desde el MVR.
        /// </summary>
        /// <remarks>
        /// Actúa como proxy de imágenes: descarga el fotograma o el recorte desde el MVR y lo devuelve al cliente,
        /// de modo que el navegador nunca accede directamente al equipo ni necesita sus credenciales.
        ///
        /// Consideraciones y limitaciones:
        /// - Los tres parámetros no deben construirse a mano. Son los que ya vienen incluidos en las rutas
        ///   <c>imagenCompleta</c> e <c>imagenRecorte</c> de <c>POST /acuseek/busqueda/estado</c>: basta usar esas
        ///   rutas tal cual (por ejemplo, como <c>src</c> de una etiqueta de imagen).
        /// - Las imágenes están ligadas a la tarea de búsqueda: si la tarea se elimina o el MVR la descarta, la
        ///   descarga deja de funcionar y se responde 502.
        /// - La respuesta es el binario de la imagen (normalmente <c>image/jpeg</c>), no JSON. El tipo de contenido
        ///   lo determina el MVR.
        /// - La descarga pasa por el servidor de la API: imágenes grandes consumen ancho de banda del servidor y
        ///   no se almacenan en caché.
        /// - Solo se admiten descargas del MVR configurado; cualquier otro destino se rechaza.
        /// </remarks>
        /// <param name="track">Identificador de pista de la imagen en el MVR, tomado de la ruta devuelta en los resultados. Obligatorio.</param>
        /// <param name="name">Nombre del archivo de imagen en el MVR, tomado de la ruta devuelta en los resultados. Obligatorio.</param>
        /// <param name="size">Tamaño de la imagen en el MVR, tomado de la ruta devuelta en los resultados. Obligatorio.</param>
        /// <param name="ct">Token de cancelación gestionado por el framework.</param>
        /// <response code="200">Binario de la imagen solicitada.</response>
        /// <response code="400">Falta alguno de los parámetros obligatorios.</response>
        /// <response code="502">La imagen ya no está disponible en el MVR o no fue posible comunicarse con él.</response>
        [HttpGet]
        [Route("imagen")]
        [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK, "image/jpeg")]
        [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status502BadGateway)]
        public async Task<IActionResult> ObtenerImagen(
            [Required(ErrorMessage = "El parametro '{0}' es obligatorio")]
            string track,
            [Required(ErrorMessage = "El parametro '{0}' es obligatorio")]
            string name,
            [Required(ErrorMessage = "El parametro '{0}' es obligatorio")]
            string size,
            CancellationToken ct)
        {
            var resultado = await _acuseekService.ObtenerImagenAsync(track, name, size, ct);
            if (!resultado.Exito)
                return ErrorMvr(resultado.Error);

            return File(resultado.Valor.Bytes, resultado.Valor.ContentType);
        }

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
