using System.Xml.Linq;
using System.Xml.XPath;
using ViisionRemolques.Enums;
using ViisionRemolques.Parsing.Models;

namespace ViisionRemolques.Parsing.Extractors
{
    //**
    //  PDC: Alerta que se genera cuando un cambio en la cantidad de personas
    //  timing: Es una alerta periodica, la cual siempre se envia cada cierto tiempo configurado. En esta ase envian la catidad de personas.
    //  trigger: Dependiendo del nivel de aglomeracion o de personas reunicas se puede enviar una misma alerta con 3 niveles de cantidad de personas.
    //  PQA: Se configurado una regla de la cantidad de personas (minimo, maximo, igual, diferente, etc) y si se cumple se genera la alerta
    //  DSA: Se configurado una regla de cuantos segundos de permanencia en el lugar (minimo, maximo, igual, diferente, etc) y si se cumple se genera la alerta
    //*/
    public class EventoAlarmaRecuentoPersonasExtractor : ICameraEventSectionExtractor
    {
        public VCAModoEnum VCAModo { get; set; } = VCAModoEnum.AlarmaRecuentoPersonas;

        private static readonly string[] EventosAplicables =
        [
            EventoAlarmaRecuentoPersonasEnum.PersonDensityDetection,

            // Eventos camara ojo de pez
            EventoAlarmaRecuentoPersonasEnum.PersonQueueTime,
            EventoAlarmaRecuentoPersonasEnum.PersonQueueCounting,
            EventoAlarmaRecuentoPersonasEnum.PersonQueueRealTime,
            EventoAlarmaRecuentoPersonasEnum.PersonQueueTimingStatistics
        ];

        public bool AplicaPara(string eventType, XDocument? doc = null) =>
            EventosAplicables.Contains(eventType, StringComparer.OrdinalIgnoreCase);

        public void Extraer(XDocument doc, EventoExtractorModelo evento)
        {

            if (evento.Evento.EventType == EventoAlarmaRecuentoPersonasEnum.PersonDensityDetection)
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
            } else
            {
                string? algoritmo = null;

                if (evento.Evento.EventType == EventoAlarmaRecuentoPersonasEnum.PersonQueueCounting) algoritmo = EventoAlarmaRecuentoPersonasAlgoritmosEnum.PQA;

                if (evento.Evento.EventType == EventoAlarmaRecuentoPersonasEnum.PersonQueueTime) algoritmo = EventoAlarmaRecuentoPersonasAlgoritmosEnum.DSA;

                if (evento.Evento.EventType == EventoAlarmaRecuentoPersonasEnum.PersonQueueRealTime) algoritmo = EventoAlarmaRecuentoPersonasAlgoritmosEnum.PDC;

                if (evento.Evento.EventType == EventoAlarmaRecuentoPersonasEnum.PersonQueueTimingStatistics) algoritmo = EventoAlarmaRecuentoPersonasAlgoritmosEnum.Timing;

                if (algoritmo == null) return;

                if (algoritmo == EventoAlarmaRecuentoPersonasAlgoritmosEnum.Timing)
                {
                    var nodos = doc.XPathSelectElements("//personqueuetimingstatistics/queuetimecounting");

                    foreach (var nodo in nodos)
                    {
                        evento.EventosAlarmaRecuentoPersonas.Add(new EventoAlarmaRecuentoPersonasExtractorModelo
                        {
                            Algoritmo = algoritmo,
                            RegionId = doc.Buscar(".//ruleid"),

                            CantidadPersonas = nodo.Buscar(".//rulecount")                        
                        });
                    }


                } else
                {
                    evento.EventosAlarmaRecuentoPersonas.Add(new EventoAlarmaRecuentoPersonasExtractorModelo
                    {
                        Algoritmo = algoritmo,
                        RegionId = doc.Buscar("//regioncapture/rule/ruleid", "//humancapture/rule/ruleid", "//personqueuerealtimedata/ruleid"),

                        // DSA o PQA
                        ValorCausaEvento = doc.Buscar("//regioncapture/rule/alarmcount", "//humancapture/rule/alarmtime"),
                        OperadorCausaEvento = doc.Buscar("//regioncapture/rule/counttriggertype", "//humancapture/rule/timetriggertype"),

                        RegionCoordenadas = doc.BuscarXMLaJSONInnerArrayJSON("//regioncapture/rule/region", "//humancapture/rule/region"),

                        // PDC
                        CantidadPersonas = doc.Buscar("//personqueuerealtimedata/peoplenum"),
                    });
                }
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
        public const string PersonDensityDetection = "personDensityDetection";
        public const string PersonQueueTime = "personQueueTime";
        public const string PersonQueueCounting = "personQueueCounting";
        public const string PersonQueueRealTime = "personQueueRealTime";
        public const string PersonQueueTimingStatistics = "personQueueTimingStatistics";
    }

    public static class EventoAlarmaRecuentoPersonasAlgoritmosEnum
    {
        public const string DSA = "DSA";
        public const string PQA = "PQA";
        public const string PDC = "PDC";
        public const string Trigger = "trigger";
        public const string Timing = "timing";
    }
}
