using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using ViisionRemolques.Repositories;

namespace ViisionRemolques.Controllers
{
    /// <summary>
    /// Ingesta de bitácoras y alertas enviadas por los gateways de sensores IoT.
    /// </summary>
    /// <remarks>
    /// Los gateways acumulan sus registros y los entregan por lotes, por lo que ambos endpoints reciben una
    /// colección y la insertan en una sola operación masiva contra la base de datos.
    ///
    /// Las filas se guardan siempre como pendientes de sincronización: un proceso posterior es el que las envía
    /// a la central y las marca como sincronizadas. Estos endpoints solo persisten lo recibido.
    /// </remarks>
    [ApiController]
    [Route("/iot")]
    [Produces("application/json")]
    public class IotController : ControllerBase
    {
        /// <summary>
        /// Cantidad máxima de registros admitidos en una sola petición.
        /// </summary>
        private const int MaximoPorLote = 5000;

        private readonly SensoresRepository _sensoresRepository;

        public IotController(SensoresRepository sensoresRepository)
        {
            _sensoresRepository = sensoresRepository;
        }

        /// <summary>
        /// Registro de bitácora generado por un dispositivo del gateway.
        /// </summary>
        /// <param name="GatewayId">Identificador (GUID) del gateway que reporta. Opcional.</param>
        /// <param name="LogId">Identificador del registro en el gateway de origen. Opcional.</param>
        /// <param name="LogType">Clasificación del registro en el gateway. Máximo 100 caracteres.</param>
        /// <param name="DeviceId">Identificador del dispositivo dentro del gateway. Opcional.</param>
        /// <param name="DeviceName">Nombre del dispositivo. Máximo 250 caracteres.</param>
        /// <param name="DeviceType">Tipo de dispositivo. Máximo 100 caracteres.</param>
        /// <param name="EventType">Tipo de evento registrado. Máximo 100 caracteres.</param>
        /// <param name="Description">Texto libre descriptivo del registro. Sin límite de longitud.</param>
        /// <param name="NumericValue">Lectura numérica del sensor. Se almacena con 4 decimales.</param>
        /// <param name="Unit">Unidad de la lectura numérica (p. ej. <c>°C</c>, <c>%</c>). Máximo 50 caracteres.</param>
        /// <param name="BooleanValue">Lectura booleana del sensor, para dispositivos de encendido/apagado.</param>
        /// <param name="RecordedAt">Instante en que el gateway registró la lectura. Opcional.</param>
        public record MainLogItemRequest(
            Guid? GatewayId,
            int? LogId,
            [MaxLength(100, ErrorMessage = "El parámetro '{0}' no puede superar los {1} caracteres.")]
            string? LogType,
            int? DeviceId,
            [MaxLength(250, ErrorMessage = "El parámetro '{0}' no puede superar los {1} caracteres.")]
            string? DeviceName,
            [MaxLength(100, ErrorMessage = "El parámetro '{0}' no puede superar los {1} caracteres.")]
            string? DeviceType,
            [MaxLength(100, ErrorMessage = "El parámetro '{0}' no puede superar los {1} caracteres.")]
            string? EventType,
            string? Description,
            decimal? NumericValue,
            [MaxLength(50, ErrorMessage = "El parámetro '{0}' no puede superar los {1} caracteres.")]
            string? Unit,
            bool? BooleanValue,
            DateTime? RecordedAt
        );

        /// <summary>
        /// Lote de registros de bitácora a insertar.
        /// </summary>
        /// <param name="Registros">Colección de registros. Obligatoria, con al menos 1 elemento y como máximo 5000.</param>
        public record MainLogLoteRequest(
            [Required(ErrorMessage = "El parametro '{0}' es obligatorio")]
            [MinLength(1, ErrorMessage = "Se requiere al menos un registro.")]
            [MaxLength(MaximoPorLote, ErrorMessage = "No se admiten más de {1} registros por petición.")]
            List<MainLogItemRequest> Registros
        );

        /// <summary>
        /// Inserta por lotes los registros de bitácora de un gateway de sensores.
        /// </summary>
        /// <remarks>
        /// Pensado para la descarga periódica del gateway: en lugar de una petición por lectura, se envía la
        /// colección completa y se persiste en una sola operación masiva.
        ///
        /// Consideraciones y limitaciones:
        /// - La carga es atómica: o se insertan todos los registros del lote, o no se inserta ninguno. Si falla
        ///   cualquier fila se revierte la transacción completa y se responde 500.
        /// - El lote admite entre 1 y 5000 registros. Un lote vacío o que exceda el máximo responde 400 sin
        ///   tocar la base de datos.
        /// - Todos los campos de cada registro son opcionales: el gateway envía solo lo que aplique a cada
        ///   dispositivo (una lectura numérica con su unidad, o una booleana, o solo una descripción).
        /// - Las longitudes de texto se validan contra el ancho de cada columna. Un texto más largo responde 400,
        ///   en lugar de fallar durante la inserción masiva.
        /// - No hay control de duplicados: reenviar el mismo lote inserta las filas otra vez. El <c>LogId</c> del
        ///   gateway se guarda como dato, no se usa como clave, así que la operación no es idempotente.
        /// - Las filas se insertan siempre como no sincronizadas; el envío a la central lo hace un proceso aparte.
        /// - <c>RecordedAt</c> se guarda tal como lo envía el gateway, sin convertir zonas horarias.
        /// - La respuesta 200 no devuelve cuerpo: confirma que el lote completo quedó guardado, sin detallar las
        ///   filas. La inserción masiva tampoco recupera los <c>IdInterno</c> generados.
        /// </remarks>
        /// <param name="request">Lote de registros de bitácora.</param>
        /// <response code="200">El lote se insertó completo.</response>
        /// <response code="400">El lote viene vacío, excede los 5000 registros o algún texto supera el largo permitido.</response>
        /// <response code="500">No fue posible guardar el lote; no se insertó ninguna fila.</response>
        [HttpPost]
        [Route("main-alert")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> MainAlert([FromBody] MainLogLoteRequest request)
        {
            var entidades = request.Registros.Select(item => new SensorMainLogEntity
            {
                GatewayId = item.GatewayId,
                LogId = item.LogId,
                LogType = item.LogType,
                DeviceId = item.DeviceId,
                DeviceName = item.DeviceName,
                DeviceType = item.DeviceType,
                EventType = item.EventType,
                Description = item.Description,
                NumericValue = item.NumericValue,
                Unit = item.Unit,
                BooleanValue = item.BooleanValue,
                RecordedAt = item.RecordedAt
            }).ToList();

            var resultado = await _sensoresRepository.InsertarMainLogsAsync(entidades);

            if (!resultado.Exito)
                return ErrorAlmacenamiento(resultado.Error);

            return Ok();
        }

        /// <summary>
        /// Alerta generada por el gateway al cruzar un umbral configurado.
        /// </summary>
        /// <param name="GatewayId">Identificador (GUID) del gateway que reporta. Opcional.</param>
        /// <param name="AlertId">Identificador de la alerta en el gateway de origen. Opcional.</param>
        /// <param name="AlertName">Nombre de la alerta. Máximo 250 caracteres.</param>
        /// <param name="LogType">Clasificación del registro que originó la alerta. Máximo 100 caracteres.</param>
        /// <param name="LogId">Identificador del registro de bitácora que originó la alerta. Opcional.</param>
        /// <param name="DeviceId">Identificador del dispositivo que disparó la alerta. Opcional.</param>
        /// <param name="ConfigAlertId">Identificador de la configuración de alerta aplicada. Opcional.</param>
        /// <param name="AlertType">Tipo de alerta. Máximo 100 caracteres.</param>
        /// <param name="ConfigName">Nombre de la configuración de alerta. Máximo 250 caracteres.</param>
        /// <param name="ConfigDescription">Descripción de la configuración de alerta. Sin límite de longitud.</param>
        /// <param name="ThresholdValue">Umbral configurado que se cruzó. Se almacena con 4 decimales.</param>
        /// <param name="MeasuredValue">Valor medido que disparó la alerta. Se almacena con 4 decimales.</param>
        /// <param name="Unit">Unidad del umbral y del valor medido. Máximo 50 caracteres.</param>
        /// <param name="CreatedAt">Instante en que el gateway generó la alerta. Opcional.</param>
        public record LogAlertItemRequest(
            Guid? GatewayId,
            int? AlertId,
            [MaxLength(250, ErrorMessage = "El parámetro '{0}' no puede superar los {1} caracteres.")]
            string? AlertName,
            [MaxLength(100, ErrorMessage = "El parámetro '{0}' no puede superar los {1} caracteres.")]
            string? LogType,
            int? LogId,
            int? DeviceId,
            int? ConfigAlertId,
            [MaxLength(100, ErrorMessage = "El parámetro '{0}' no puede superar los {1} caracteres.")]
            string? AlertType,
            [MaxLength(250, ErrorMessage = "El parámetro '{0}' no puede superar los {1} caracteres.")]
            string? ConfigName,
            string? ConfigDescription,
            decimal? ThresholdValue,
            decimal? MeasuredValue,
            [MaxLength(50, ErrorMessage = "El parámetro '{0}' no puede superar los {1} caracteres.")]
            string? Unit,
            DateTime? CreatedAt
        );

        /// <summary>
        /// Lote de alertas a insertar.
        /// </summary>
        /// <param name="Alertas">Colección de alertas. Obligatoria, con al menos 1 elemento y como máximo 5000.</param>
        public record LogAlertLoteRequest(
            [Required(ErrorMessage = "El parametro '{0}' es obligatorio")]
            [MinLength(1, ErrorMessage = "Se requiere al menos una alerta.")]
            [MaxLength(MaximoPorLote, ErrorMessage = "No se admiten más de {1} alertas por petición.")]
            List<LogAlertItemRequest> Alertas
        );

        /// <summary>
        /// Inserta por lotes las alertas generadas por un gateway de sensores.
        /// </summary>
        /// <remarks>
        /// Mismo modelo de ingesta que la bitácora, pero para las alertas que el gateway dispara al cruzarse un
        /// umbral configurado.
        ///
        /// Consideraciones y limitaciones:
        /// - La carga es atómica: o se insertan todas las alertas del lote, o no se inserta ninguna. Si falla
        ///   cualquier fila se revierte la transacción completa y se responde 500.
        /// - El lote admite entre 1 y 5000 alertas. Un lote vacío o que exceda el máximo responde 400 sin tocar
        ///   la base de datos.
        /// - Todos los campos de cada alerta son opcionales; se guarda lo que el gateway reporte.
        /// - Las longitudes de texto se validan contra el ancho de cada columna. Un texto más largo responde 400,
        ///   en lugar de fallar durante la inserción masiva.
        /// - No hay control de duplicados ni validación referencial: el <c>LogId</c> se guarda como dato y no se
        ///   comprueba que exista el registro de bitácora correspondiente. Reenviar el mismo lote duplica filas.
        /// - Las alertas se insertan siempre como no sincronizadas; el envío a la central lo hace un proceso aparte.
        /// - <c>CreatedAt</c> se guarda tal como lo envía el gateway, sin convertir zonas horarias.
        /// - La respuesta 200 no devuelve cuerpo: confirma que el lote completo quedó guardado, sin detallar las
        ///   filas. La inserción masiva tampoco recupera los <c>IdInterno</c> generados.
        /// </remarks>
        /// <param name="request">Lote de alertas.</param>
        /// <response code="200">El lote se insertó completo.</response>
        /// <response code="400">El lote viene vacío, excede las 5000 alertas o algún texto supera el largo permitido.</response>
        /// <response code="500">No fue posible guardar el lote; no se insertó ninguna fila.</response>
        [HttpPost]
        [Route("log-alert")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> LogAlert([FromBody] LogAlertLoteRequest request)
        {
            var entidades = request.Alertas.Select(item => new SensorLogAlertEntity
            {
                GatewayId = item.GatewayId,
                AlertId = item.AlertId,
                AlertName = item.AlertName,
                LogType = item.LogType,
                LogId = item.LogId,
                DeviceId = item.DeviceId,
                ConfigAlertId = item.ConfigAlertId,
                AlertType = item.AlertType,
                ConfigName = item.ConfigName,
                ConfigDescription = item.ConfigDescription,
                ThresholdValue = item.ThresholdValue,
                MeasuredValue = item.MeasuredValue,
                Unit = item.Unit,
                CreatedAt = item.CreatedAt
            }).ToList();

            var resultado = await _sensoresRepository.InsertarAlertasAsync(entidades);

            if (!resultado.Exito)
                return ErrorAlmacenamiento(resultado.Error);

            return Ok();
        }

        private IActionResult ErrorAlmacenamiento(string? error) =>
            Problem(
                detail: error ?? "No fue posible guardar la información recibida.",
                statusCode: StatusCodes.Status500InternalServerError,
                title: "Error al guardar el lote"
            );
    }
}
