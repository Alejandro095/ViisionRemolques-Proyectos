using System.Xml.Linq;
using ViisionRemolques.Enums;
using ViisionRemolques.Parsing.Models;

namespace ViisionRemolques.Parsing.Extractors
{
    public class EventoAlarmaRecuentoPersonasExtractor : ICameraEventSectionExtractor
    {
        public VCAModoEnum VCAModo { get; set; } = VCAModoEnum.AlarmaRecuentoPersonas;

        private static readonly string[] EventosAplicables =
        [
            "persondensitydetection"
        ];

        public bool AplicaPara(string eventType, XDocument? doc = null) =>
            EventosAplicables.Contains(eventType, StringComparer.OrdinalIgnoreCase);

        public void Extraer(XDocument doc, EventoExtractorModelo evento)
        {
            evento.EventoAlarmaRecuentoPersonas = new EventoAlarmaRecuentoPersonasExtractorModelo
            {
                ObjetivoDetectadoTipo = doc.Buscar("//persondensityresult//recognitiontype"),
                Algoritmo = doc.Buscar("//targetinfo/datasource"),
                RegionId = doc.Buscar("//targetinfo/regionid"),
                RegionCoordenadas = doc.BuscarXMLaJSONInnerArrayJSON("//targetinfo//region"),

                ValorCausaEvento = doc.Buscar("//dsa/alarmtime", "//pqa/alarmcount"),
                OperadorCausaEvento = doc.Buscar("//dsa/timetriggertype", "//pqa/counttriggertype"),

                CantidadPersonas = doc.Buscar("//targetinfo/personcnt", "//targetinfo/framespeoplecounting_number"),
                NivelDensidad = doc.Buscar("//targetinfo/densitylevel"),
                NombreNivelDensidad = doc.Buscar("//targetinfo/customname"),
                DireccionCambioDensidad = doc.Buscar("//targetinfo/densitylevelchangetype"),
            };
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
}
