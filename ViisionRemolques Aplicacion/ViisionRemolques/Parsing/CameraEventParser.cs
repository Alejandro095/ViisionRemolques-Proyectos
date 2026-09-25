using ViisionRemolques.Parsing.Extractors;
using ViisionRemolques.Parsing.Models;

namespace ViisionRemolques.Parsing
{
    /// <summary>
    /// Punto de entrada del parseo. Normaliza el cuerpo (XML o JSON), extrae la
    /// información base y después ejecuta los extractores que apliquen al tipo de evento.
    /// </summary>
    public static class CameraEventParser
    {
        private static readonly EventoBaseExtractor InfoBase = new();

        private static readonly List<ICameraEventSectionExtractor> Extractores =
        [
            new EventoSmartExtractor(),
            new EventoAlarmaRecuentoPersonasExtractor(),
            new EventoRecuentoPersonasExtractor(),
            new EventoCapturaFacialExtractor(),
            new EventoDeteccionTipoMultiobjetivoExtractor(),
        ];

        /// <returns>
        /// El evento interpretado, o <c>null</c> si el cuerpo no se pudo leer.
        /// Nunca lanza: un payload ilegible no debe tumbar el webhook.
        /// </returns>
        public static EventoExtractorModelo? Parse(string? cuerpo)
        {
            var doc = CameraPayloadLoader.ToNormalizedXml(cuerpo);

            if (doc is null) return null;

            var evento = new EventoExtractorModelo();

            InfoBase.Extraer(doc, evento);

            var tipoEvento = evento.Evento.EventType ?? string.Empty;

            foreach (var extractor in Extractores)
            {
                if (extractor.AplicaPara(tipoEvento, doc))
                {
                    extractor.Extraer(doc, evento);

                    evento.Evento.VCAModo = extractor.VCAModo;

                    break;
                }
            }

            return evento;
        }
    }
}
