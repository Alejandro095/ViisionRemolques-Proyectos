using System.Xml.Linq;
using ViisionRemolques.Enums;
using ViisionRemolques.Parsing.Models;

namespace ViisionRemolques.Parsing.Extractors
{
    public class EventoAlarmaConteoPersonasExtractor : ICameraEventSectionExtractor
    {
        public VCAModoEnum VCAModo { get; set; } = VCAModoEnum.AlarmaConteoPersonas;

        private static readonly string[] EventosAplicables =
        [
            "peoplecounting"
        ];

        public bool AplicaPara(string eventType, XDocument? doc = null) =>
            EventosAplicables.Contains(eventType, StringComparer.OrdinalIgnoreCase);

        public void Extraer(XDocument doc, CameraEventModel evento)
        {
            evento.AlarmaConteoPersonas = new AlarmaConteoPersonasModel
            {
                Regiones = doc.BuscarInnerArrayJson("//regionlist"),
                TotalEntradas = doc.Buscar("//peoplecounting/enter"),
                TotalSalidas = doc.Buscar("//peoplecounting/exit"),
                TotalPasos = doc.Buscar("//peoplecounting/pass"),
                TotalDuplicados = doc.Buscar("//peoplecounting/duplicatepeople"),
            };
        }
    }
}
