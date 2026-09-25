using System.Xml.Linq;
using ViisionRemolques.Enums;
using ViisionRemolques.Parsing.Models;

namespace ViisionRemolques.Parsing.Extractors
{
    public class EventoDeteccionTipoMultiobjetivoExtractor : ICameraEventSectionExtractor
    {
        public VCAModoEnum VCAModo { get; set; } = VCAModoEnum.RecuentoPersonas;

        private static readonly string[] EventosAplicables =
        [
            "mixedTargetDetection"
        ];

        public bool AplicaPara(string eventType, XDocument? doc = null) =>
            EventosAplicables.Contains(eventType, StringComparer.OrdinalIgnoreCase);

        public void Extraer(XDocument doc, EventoExtractorModelo evento)
        {
            //evento.AlarmaConteoPersonas = new AlarmaConteoPersonasModel
            //{                
            //    TotalEntradas = doc.Buscar("//peoplecounting/enter"),
            //    TotalSalidas = doc.Buscar("//peoplecounting/exit"),
            //    TotalPasos = doc.Buscar("//peoplecounting/pass"),
            //    TotalDuplicados = doc.Buscar("//peoplecounting/duplicatepeople"),

            //    Regiones = doc.BuscarInnerArrayJson("//regionlist"),
            //};
        }
    }
}
