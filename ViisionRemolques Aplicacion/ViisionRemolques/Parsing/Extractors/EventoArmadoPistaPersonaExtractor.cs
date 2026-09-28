using System.Xml.Linq;
using ViisionRemolques.Enums;
using ViisionRemolques.Parsing.Models;

namespace ViisionRemolques.Parsing.Extractors
{
    public class EventoArmadoPistaPersonaExtractor : ICameraEventSectionExtractor
    {
        public VCAModoEnum VCAModo { get; set; } = VCAModoEnum.ArmadoPistaPersona;

        private static readonly string[] EventosAplicables =
        [
            EventoArmadoPistaPersonaEnum.ArmadoPistaPersona,
        ];

        public bool AplicaPara(string eventType, XDocument? doc = null)
        {

            return EventosAplicables.Contains(eventType, StringComparer.OrdinalIgnoreCase)
                   && doc != null 
                   && doc.Buscar("//persistenteventstatus") == "started";
        }

        public void Extraer(XDocument doc, EventoExtractorModelo evento)
        {
            var fdid = doc.Buscar(
                "//personArmingtrackinfo/personinfo/face/facecontrastresult/faceappenddata/certificatenumber",
                "//facecontrastresult/faceappenddata/certificatenumber"
            );

            var item = new EventoArmadoPistaPersonaExtractorModelo
            {
                Edad = doc.Buscar(
                    "//personArmingtrackinfo/personinfo/face/facecaptureresult/age/value",
                    "//facecaptureresult/age/value"
                ),
                GrupoEdad = doc.Buscar(
                    "//personArmingtrackinfo/personinfo/face/facecaptureresult/age/agegroup",
                    "//facecaptureresult/age/agegroup"
                ),
                Genero = doc.Buscar(
                    "//personArmingtrackinfo/personinfo/face/facecaptureresult/gender/value",
                    "//facecaptureresult/gender/value"
                ),
                Lentes = doc.Buscar(
                    "//personArmingtrackinfo/personinfo/face/facecaptureresult/glasses/value",
                    "//facecaptureresult/glasses/value"
                ),
                Mascara = doc.Buscar(
                    "//personArmingtrackinfo/personinfo/face/facecaptureresult/mask/value",
                    "//facecaptureresult/mask/value"
                ),
                ExpresionFacial = doc.Buscar(
                    "//personArmingtrackinfo/personinfo/face/facecaptureresult/faceexpression/value",
                    "//facecaptureresult/faceexpression/value"
                ),
                Sombrero = doc.Buscar(
                    "//personArmingtrackinfo/personinfo/face/facecaptureresult/hat/value",
                    "//facecaptureresult/hat/value"
                ),

                // Deteccion facial
                DeteccionFacialId = fdid,
                DeteccionFacial = !string.IsNullOrEmpty(fdid)
            };

            evento.EventosArmadoPistaPersona.Add(item);
        }
    }

    public class EventoArmadoPistaPersonaExtractorModelo
    {
        public string? Edad {  get; set; }
        public string? GrupoEdad { get; set; }

        public string? Genero { get; set; }
        public string? Lentes { get; set; }
        public string? Mascara { get; set; }
        public string? ExpresionFacial { get; set; }
        public string? Sombrero { get; set; }

        // Deteccion facial
        public bool DeteccionFacial { get; set; } = false;
        public string? DeteccionFacialId { get; set; }
    }

    public static class EventoArmadoPistaPersonaEnum
    {
        public static readonly string ArmadoPistaPersona = "personArmingTrack";
    }
}
