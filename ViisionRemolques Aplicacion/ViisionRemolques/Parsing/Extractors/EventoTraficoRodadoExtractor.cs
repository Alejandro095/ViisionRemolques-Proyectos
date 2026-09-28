using System.Xml.Linq;
using System.Xml.XPath;
using ViisionRemolques.Enums;
using ViisionRemolques.Parsing.Models;

namespace ViisionRemolques.Parsing.Extractors
{
    public class EventoTraficoRodadoExtractor : ICameraEventSectionExtractor
    {
        public VCAModoEnum VCAModo { get; set; } = VCAModoEnum.TraficoRodado;

        private static readonly string[] EventosAplicables =
        [
            EventoTraficoRodadoEnum.TPSRealTime,
        ];

        public bool AplicaPara(string eventType, XDocument? doc = null) =>
            EventosAplicables.Contains(eventType, StringComparer.OrdinalIgnoreCase);

        public void Extraer(XDocument doc, EventoExtractorModelo evento)
        {
            // Acumuladores para Vehículos
            int vehiculosVolumen = 0;
            int vehiculosDownwardFlow = 0;
            int vehiculosUpwardFlow = 0;

            // Acumuladores para Motocicletas
            int motocicletasVolumen = 0;
            int motocicletasDownwardFlow = 0;
            int motocicletasUpwardFlow = 0;

            foreach (var nodoTarget in doc.SeleccionarElementos("//target"))
            {
                var recognitionType = nodoTarget.ValorElemento("./recognitiontype");

                int sumaVolumTarget = 0;
                int sumaDownwardTarget = 0;
                int sumaUpwardTarget = 0;

                // Iteramos los carriles dentro del TargetInfo de este Target
                foreach (var nodoLane in nodoTarget.XPathSelectElements("./targetinfo/laneinfo"))
                {
                    if (int.TryParse(nodoLane.ValorElemento("./volum"), out int volum))
                    {
                        sumaVolumTarget += volum;
                    }

                    if (int.TryParse(nodoLane.ValorElemento("./downwardflow"), out int downward))
                    {
                        sumaDownwardTarget += downward;
                    }

                    if (int.TryParse(nodoLane.ValorElemento("./upwardflow"), out int upward))
                    {
                        sumaUpwardTarget += upward;
                    }
                }

                // Sumamos al total según el tipo de reconocimiento
                if (string.Equals(recognitionType, "vehicle", StringComparison.OrdinalIgnoreCase))
                {
                    vehiculosVolumen += sumaVolumTarget;
                    vehiculosDownwardFlow += sumaDownwardTarget;
                    vehiculosUpwardFlow += sumaUpwardTarget;
                }
                else if (string.Equals(recognitionType, "motorcycle", StringComparison.OrdinalIgnoreCase))
                {
                    motocicletasVolumen += sumaVolumTarget;
                    motocicletasDownwardFlow += sumaDownwardTarget;
                    motocicletasUpwardFlow += sumaUpwardTarget;
                }
            }

            evento.EventoTraficoRodado = new EventoTraficoRodadoExtractorModelo
            {
                VehiculosTotal= vehiculosVolumen,
                VehiculosFlujoAscendente = vehiculosUpwardFlow,
                VehiculosFlujoDescendente = vehiculosDownwardFlow,

                MotocicletasTotal = motocicletasVolumen,
                MotocicletasFlujoAscendente = motocicletasUpwardFlow,
                MotocicletasFlujoDescendente = motocicletasDownwardFlow,
            };
        }
    }

    public class EventoTraficoRodadoExtractorModelo
    {
        // Vehículos
        public int? VehiculosTotal { get; set; }
        public int? VehiculosFlujoAscendente { get; set; }
        public int? VehiculosFlujoDescendente { get; set; }

        // Motocicletas
        public int? MotocicletasTotal { get; set; }
        public int? MotocicletasFlujoAscendente { get; set; }
        public int? MotocicletasFlujoDescendente { get; set; }
    }

    public static class EventoTraficoRodadoEnum
    {
        public static readonly string TPSRealTime = "TPSRealTime";
    }
}
