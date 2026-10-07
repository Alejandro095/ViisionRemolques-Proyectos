using ViisionRemolques.Parsing.Extractors;
using ViisionRemolques.Parsing.Models;

namespace ViisionRemolques.Parsing
{
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
            new EventoANPRExtractor(),
            new EventoArmadoPistaPersonaExtractor(),
            new EventoTraficoRodadoExtractor(),
        ];

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
