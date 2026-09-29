using System.Xml.Linq;
using System.Xml.XPath;
using ViisionRemolques.Enums;
using ViisionRemolques.Parsing.Models;

namespace ViisionRemolques.Parsing.Extractors
{
    public class EventoAlarmaRecuentoPersonasExtractor : ICameraEventSectionExtractor
    {
        public VCAModoEnum VCAModo { get; set; } = VCAModoEnum.AlarmaRecuentoPersonas;

        private static readonly string[] EventosAplicables =
        [
            EventoAlarmaRecuentoPersonasEnum.PersonDensityDetection
        ];

        public bool AplicaPara(string eventType, XDocument? doc = null) =>
            EventosAplicables.Contains(eventType, StringComparer.OrdinalIgnoreCase);

        public void Extraer(XDocument doc, EventoExtractorModelo evento)
        {

            var nodos = doc.XPathSelectElements("//persondensityresult/target");

            foreach (var nodo in nodos)
            {
                evento.EventosAlarmaRecuentoPersonas.Add(new EventoAlarmaRecuentoPersonasExtractorModelo
                {
                    ObjetivoDetectadoTipo = nodo.Buscar("./recognitiontype"),

                    Algoritmo = nodo.Buscar("./targetinfo/datasource"),
                    RegionId = nodo.Buscar("./targetinfo/regionid"),

                    // DSA o PQA
                    ValorCausaEvento = nodo.Buscar("./targetinfo/dsa/alarmtime", "./targetinfo/pqa/alarmcount"),
                    OperadorCausaEvento = nodo.Buscar("./targetinfo/dsa/timetriggertype", "./targetinfo/pqa/counttriggertype"),

                    RegionCoordenadas = nodo.BuscarXMLaJSONInnerArrayJSON("./targetinfo/dsa/region", "./targetinfo/pqa/region"),

                    // Timing
                    CantidadPersonas = nodo.Buscar("./targetinfo/personcnt", ".//targetinfo/framespeoplecounting_number"), // Trigger / PDC
                    NivelDensidad = nodo.Buscar("./targetinfo/densitylevel"),
                    NombreNivelDensidad = nodo.Buscar("./targetinfo/customname"),
                    DireccionCambioDensidad = nodo.Buscar("./targetinfo/densitylevelchangetype")
                });
            }
        }
    }

    public class EventoAlarmaRecuentoPersonasExtractorModelo
    {
        public string? ObjetivoDetectadoTipo { get; set; } // Human
        public string? Algoritmo { get; set; } // Algoritmo usado (DSA: Permanencia x tiempo)
        public string? RegionId { get; set; } // Regla o regionId
        public string? RegionCoordenadas { get; set; }

        //DSA y PQA
        public string? ValorCausaEvento { get; set; }
        public string? OperadorCausaEvento { get; set; }

        //trigger
        public string? CantidadPersonas { get; set; } // PDC y timing
        public string? NivelDensidad { get; set; }
        public string? NombreNivelDensidad { get; set; }
        public string? DireccionCambioDensidad { get; set; }

    }

    public static class EventoAlarmaRecuentoPersonasEnum
    {
        public static readonly string PersonDensityDetection = "persondensitydetection";
    }

    public static class EventoAlarmaRecuentoPersonasAlgoritmosEnum
    {
        public static readonly string DSA = "DSA";
        public static readonly string PQA = "PQA";
        public static readonly string PDC = "PDC";
        public static readonly string Trigger = "trigger";
        public static readonly string Timing = "timing";
    }
}
