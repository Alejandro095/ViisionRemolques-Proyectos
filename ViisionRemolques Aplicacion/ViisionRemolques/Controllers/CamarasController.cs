using Microsoft.AspNetCore.Mvc;
using ViisionRemolques.Repositories;
using ViisionRemolques.Services.ISAPI;

namespace ViisionRemolques.Controllers
{
    /// <summary>
    /// Inventario de cámaras registradas en la plataforma y las funcionalidades que soporta cada una.
    /// </summary>
    [ApiController]
    [Route("/camaras")]
    [Produces("application/json")]
    public class CamarasController : ControllerBase
    {
        private readonly CamaraRepository _camaraRepository;
        public CamarasController(CamaraRepository camaraRepository) {

            _camaraRepository = camaraRepository;
        }

        /// <summary>
        /// Capacidades PTZ declaradas para la cámara.
        /// </summary>
        /// <param name="Movimiento">La cámara admite movimiento continuo de paneo y tilt (<c>PUT /camaras/{idInterno}/ptz/mover/{pan}/{tilt}</c>).</param>
        /// <param name="Zoom">La cámara admite zoom continuo (<c>PUT /camaras/{idInterno}/ptz/zoom/{valor}</c>).</param>
        /// <param name="Enfoque">La cámara admite enfoque manual (<c>PUT /camaras/{idInterno}/ptz/enfoque/{valor}</c>).</param>
        /// <param name="Iris">La cámara admite apertura manual del iris (<c>PUT /camaras/{idInterno}/ptz/iris/{valor}</c>).</param>
        /// <param name="Escobilla">La cámara tiene limpiaparabrisas accionable (<c>PUT /camaras/{idInterno}/ptz/escobilla</c>).</param>
        /// <param name="EnfoqueAuxiliar">La cámara admite enfoque automático de una pulsación (<c>PUT /camaras/{idInterno}/ptz/enfoque-auxiliar</c>).</param>
        /// <param name="InicializacionObjetivo">La cámara admite reiniciar el objetivo a su posición de fábrica (<c>PUT /camaras/{idInterno}/ptz/inicializacion-objetivo</c>).</param>
        /// <param name="CalibracionZoom">La cámara admite recalibrar el recorrido del zoom (<c>PUT /camaras/{idInterno}/ptz/calibracion-zoom</c>).</param>
        public record PtzFuncionalidadesResponse(
            bool Movimiento,
            bool Zoom,
            bool Enfoque,
            bool Iris,
            bool Escobilla,
            bool EnfoqueAuxiliar,
            bool InicializacionObjetivo,
            bool CalibracionZoom
        );

        /// <summary>
        /// Funcionalidades soportadas por la cámara.
        /// </summary>
        /// <param name="AudioBidireccional">La cámara dispone de audio de ida y vuelta (altavoz + micrófono).</param>
        /// <param name="PTZ">Detalle de las capacidades PTZ disponibles.</param>
        public record FuncionalidadesResponse(
            bool AudioBidireccional,
            PtzFuncionalidadesResponse PTZ
        );

        /// <summary>
        /// Cámara registrada en la plataforma.
        /// </summary>
        /// <param name="IdInterno">Identificador interno de la cámara. Es el valor que se usa como <c>idInterno</c> en los endpoints de PTZ, VCA y video.</param>
        /// <param name="Nombre">Nombre descriptivo asignado a la cámara.</param>
        /// <param name="Modelo">Modelo del fabricante (p. ej. <c>DS-2DF8C842IXG1-ELWY</c>). Determina qué plataforma ISAPI se utiliza internamente.</param>
        /// <param name="Activo">Siempre <c>true</c>: el listado solo devuelve cámaras activas.</param>
        /// <param name="Funcionalidades">Capacidades soportadas por la cámara.</param>
        public record CamaraResponse(
            long IdInterno,
            string Nombre,
            string Modelo,
            bool Activo,
            FuncionalidadesResponse Funcionalidades
        );

        /// <summary>
        /// Lista las cámaras disponibles con sus funcionalidades.
        /// </summary>
        /// <remarks>
        /// Devuelve el inventario completo de cámaras que la plataforma puede operar. Es el punto de partida
        /// para el resto de la API: el campo <c>idInterno</c> de cada elemento es el identificador que requieren
        /// los endpoints de PTZ (<c>/camaras/{idInterno}/ptz/...</c>), VCA (<c>/camaras/{idInterno}/vca/...</c>)
        /// y de token de streaming (<c>/video/token/stream</c>).
        ///
        /// Consideraciones y limitaciones:
        /// - Solo se devuelven cámaras marcadas como activas en la base de datos; las dadas de baja no aparecen
        ///   y sus identificadores responderán 404 en el resto de endpoints.
        /// - El orden es por fecha de creación descendente (las más recientes primero).
        /// - El bloque <c>funcionalidades</c> refleja lo declarado en el registro de la cámara, no una consulta
        ///   en vivo al dispositivo. Antes de invocar una acción PTZ conviene comprobar la bandera correspondiente:
        ///   si está en <c>false</c>, el endpoint de PTZ responderá 400 indicando que la cámara no soporta esa acción.
        /// - El soporte de VCA no se expone aquí porque depende de la plataforma del dispositivo; para conocer
        ///   los modos disponibles se debe consultar <c>GET /camaras/{idInterno}/vca/modos</c>.
        /// - No requiere parámetros y no está paginado: el parque de cámaras es reducido y se devuelve completo.
        /// - La operación no contacta a las cámaras, únicamente lee la base de datos, por lo que responde aunque
        ///   los dispositivos estén fuera de línea.
        /// </remarks>
        /// <response code="200">Listado de cámaras activas. Puede ser una colección vacía si no hay ninguna registrada.</response>
        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<CamaraResponse>), StatusCodes.Status200OK)]
        public async Task<IActionResult> Listar()
        {
            var camaras = await _camaraRepository.ObtenerTodasAsync();

            return Ok(camaras.Select(c => new CamaraResponse(
                c.IdInterno,
                c.Nombre,
                c.Modelo,
                c.Activo,
                new FuncionalidadesResponse(
                    AudioBidireccional: c.Soporta_AudioBidireccional,
                    PTZ: new PtzFuncionalidadesResponse(
                        Movimiento: c.Soporta_PtzMovimiento,
                        Zoom: c.Soporta_PtzZoom,
                        Enfoque: c.Soporta_PtzEnfoque,
                        Iris: c.Soporta_PtzIris,
                        Escobilla: c.Soporta_PtzEscobilla,
                        EnfoqueAuxiliar: c.Soporta_PtzEnfoqueAuxiliar,
                        InicializacionObjetivo: c.Soporta_PtzInicializacionObjetivo,
                        CalibracionZoom: c.Soporta_PtzCalibracionZoom
                    )
                )
            )));
        }
    }
}
