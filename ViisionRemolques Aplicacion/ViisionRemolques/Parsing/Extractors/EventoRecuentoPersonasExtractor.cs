using System.Xml.Linq;
using System.Xml.XPath;
using ViisionRemolques.Enums;
using ViisionRemolques.Parsing.Models;

namespace ViisionRemolques.Parsing.Extractors
{
    public class EventoRecuentoPersonasExtractor : ICameraEventSectionExtractor
    {
        public VCAModoEnum VCAModo { get; set; } = VCAModoEnum.RecuentoPersonas;

        private static readonly string[] EventosAplicables =
        [
            EventoRecuentoPersonasEnum.RecuentoPersonas
        ];

        public bool AplicaPara(string eventType, XDocument? doc = null) =>
            EventosAplicables.Contains(eventType, StringComparer.OrdinalIgnoreCase);

        public void Extraer(XDocument doc, EventoExtractorModelo evento)
        {

            // Totales generales (todas las regiones)
            var todasEntradas = doc.Buscar("//peoplecounting/enter");
            var todasSalidas = doc.Buscar("//peoplecounting/exit");
            var todasTranseuntes = doc.Buscar("//peoplecounting/pass");
            var todasDuplicados = doc.Buscar("//peoplecounting/duplicatepeople");

            var nodosRegion = doc.XPathSelectElements("//regionlist/region").ToList();

            if (nodosRegion.Any())
            {
                // Un registro por cada región específica encontrada
                foreach (var nodoRegion in nodosRegion)
                {
                    evento.EventosRecuentoPersonas.Add(new EventoRecuentoPersonasExtractorModelo
                    {
                        TodasRegionesEntradas = todasEntradas,
                        TodasRegionesSalidas = todasSalidas,
                        TodasRegionesTranseuntes = todasTranseuntes,
                        TodasRegionesDuplicados = todasDuplicados,

                        RegionId = nodoRegion.Buscar("./id"),
                        RegionEntradas = nodoRegion.Buscar("./enter"),
                        RegionSalidas = nodoRegion.Buscar("./exit"),
                        RegionTranseuntes = nodoRegion.Buscar("./pass"),
                    });
                }
            }
            else
            {
                // Fallback: Si no vienen regiones individuales, generamos un único registro con los datos globales
                evento.EventosRecuentoPersonas.Add(new EventoRecuentoPersonasExtractorModelo
                {
                    TodasRegionesEntradas = todasEntradas,
                    TodasRegionesSalidas = todasSalidas,
                    TodasRegionesTranseuntes = todasTranseuntes,
                    TodasRegionesDuplicados = todasDuplicados
                });
            }
        }
    }

    public class EventoRecuentoPersonasExtractorModelo
    {
        public string? TodasRegionesEntradas { get; set; }
        public string? TodasRegionesSalidas { get; set; }
        public string? TodasRegionesTranseuntes { get; set; }
        public string? TodasRegionesDuplicados { get; set; }

        public string? RegionId { get; set; }
        public string? RegionEntradas { get; set; }
        public string? RegionSalidas { get; set; }
        public string? RegionTranseuntes { get; set; }
    }

    public static class EventoRecuentoPersonasEnum
    {
        public static readonly string RecuentoPersonas = "peoplecounting";
    }
}
