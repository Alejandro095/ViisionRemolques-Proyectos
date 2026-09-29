using System.Threading.Tasks.Dataflow;
using System.Xml.Linq;
using System.Xml.XPath;
using ViisionRemolques.Enums;
using ViisionRemolques.Parsing.Models;

namespace ViisionRemolques.Parsing.Extractors
{
    public class EventoDeteccionTipoMultiobjetivoExtractor : ICameraEventSectionExtractor
    {
        public VCAModoEnum VCAModo { get; set; } = VCAModoEnum.DeteccionTipoMultiObjetivo;

        private static readonly string[] EventosAplicables =
        [
            EventoDeteccionTipoMultiobjetivoEnum.MixedTargetDetection
        ];

        public bool AplicaPara(string eventType, XDocument? doc = null) =>
            EventosAplicables.Contains(eventType, StringComparer.OrdinalIgnoreCase);

        public void Extraer(XDocument doc, EventoExtractorModelo evento)
        {
            foreach (var nodeCapture in doc.XPathSelectElements("//captureresult"))
            {
                var elementoCaptura = new EventoDeteccionTipoMultiobjectivoExtractorModelo();

                var nodoHumano = nodeCapture.XPathSelectElement("./human | ./face");
                if (nodoHumano != null)
                {
                    elementoCaptura.Tipo = EventoDeteccionTipoMultiobjetivoTiposEnum.Humano;
                    elementoCaptura.Humano = new EventoDeteccionTipoMultiobjectivoTipoHumanoExtractorModelo
                    {
                        Edad = ObtenerPropiedad(nodoHumano, "age"),
                        ExpresionFacial = ObtenerPropiedad(nodoHumano, "faceExpression"),
                        ColorChaqueta = ObtenerPropiedad(nodoHumano, "jacketColor"),
                        Lentes = ObtenerPropiedad(nodoHumano, "glass"),
                        Genero = ObtenerPropiedad(nodoHumano, "gender"),
                        Bolso = ObtenerPropiedad(nodoHumano, "bag"),
                        Sombrero = ObtenerPropiedad(nodoHumano, "hat"),
                        TipoChaqueta = ObtenerPropiedad(nodoHumano, "jacketType"),
                        Mascarilla = ObtenerPropiedad(nodoHumano, "mask"),
                        EstiloCabello = ObtenerPropiedad(nodoHumano, "hairStyle"),
                        GrupoEdad = ObtenerPropiedad(nodoHumano, "ageGroup"),
                        Objetos = ObtenerPropiedad(nodoHumano, "things"),
                        ColorPantalon = ObtenerPropiedad(nodoHumano, "trousersColor"),
                        TipoPantalon = ObtenerPropiedad(nodoHumano, "trousersType"),
                        Direcion = ObtenerPropiedad(nodoHumano, "direction"),
                        Puntuacion = ObtenerPropiedad(nodoHumano, "score"),

                        DeteccionFacial = nodeCapture.Buscar(".//facecontrastresult//faces//human_data/similarity") != null,
                        DeteccionFacialId = nodeCapture.Buscar(".//facecontrastresult//faces//reserve_field/certificatenumber"),
                    };
                }

                var nodoVehiculo = nodeCapture.XPathSelectElement("./vehicle");
                if (nodoVehiculo != null)
                {
                    elementoCaptura.Tipo = EventoDeteccionTipoMultiobjetivoTiposEnum.Vehiculo;
                    elementoCaptura.Vehiculo = new EventoDeteccionTipoMultiobjectivoTipoVehiculoExtractorModelo
                    {
                        Matricula = ObtenerPropiedad(nodoVehiculo, "plateNo"),
                        Tipo = ObtenerPropiedad(nodoVehiculo, "vehicleType"),
                        Color = ObtenerPropiedad(nodoVehiculo, "vehicleColor"),
                        Logo = ObtenerPropiedad(nodoVehiculo, "vehicleLogoString"),
                        Puntuacion = ObtenerPropiedad(nodoVehiculo, "confidence")
                    };
                }

                evento.EventosDeteccionTipoMultiobjectivo.Add(elementoCaptura);
            }
        }

        private string? ObtenerPropiedad(XElement element, string nombrePropiedad) => 
            element.Descendants("property")
                .FirstOrDefault(p => string.Equals(
                    p.Element("description")?.Value.Trim(),
                    nombrePropiedad.Trim(),
                    StringComparison.OrdinalIgnoreCase))
                ?.Element("value")?.Value.Trim();
    }

    public class EventoDeteccionTipoMultiobjectivoExtractorModelo
    {
        public string Tipo { get; set; } = EventoDeteccionTipoMultiobjetivoTiposEnum.Ninguno;

        public EventoDeteccionTipoMultiobjectivoTipoHumanoExtractorModelo? Humano { get; set; }
        public EventoDeteccionTipoMultiobjectivoTipoVehiculoExtractorModelo? Vehiculo { get; set; }
    }

    public class EventoDeteccionTipoMultiobjectivoTipoHumanoExtractorModelo
    {
        public string? Edad { get; set; }
        public string? ExpresionFacial { get; set; }
        public string? ColorChaqueta { get; set; }
        public string? Lentes { get; set; }
        public string? Genero { get; set; }
        public string? Bolso { get; set; }
        public string? Sombrero { get; set; }
        public string? TipoChaqueta { get; set; }
        public string? Mascarilla { get; set; }
        public string? EstiloCabello { get; set; }
        public string? GrupoEdad { get; set; }
        public string? Objetos { get; set; }
        public string? ColorPantalon { get; set; }
        public string? TipoPantalon { get; set; }
        public string? Direcion { get; set; }
        public string? Puntuacion { get; set; }

        // DeteccionFacial
        public bool DeteccionFacial { get; set; } = false;
        public string? DeteccionFacialId { get; set; }

    }

    public class EventoDeteccionTipoMultiobjectivoTipoVehiculoExtractorModelo
    {
        public string? Matricula { get; set; }
        public string? Tipo { get; set; }
        public string? Color{ get; set; }
        public string? Logo { get; set; }
        public string? Puntuacion { get; set; }
    }

    public static class EventoDeteccionTipoMultiobjetivoTiposEnum
    {
        public static readonly string Ninguno = "";
        public static readonly string Humano = "Humano";
        public static readonly string Vehiculo = "Vehiculo";
    }

    public static class EventoDeteccionTipoMultiobjetivoEnum
    {
        public static readonly string MixedTargetDetection = "mixedTargetDetection";
    }
}