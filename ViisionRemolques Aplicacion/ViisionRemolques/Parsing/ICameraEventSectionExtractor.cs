using System.Xml.Linq;
using ViisionRemolques.Enums;
using ViisionRemolques.Parsing.Models;

namespace ViisionRemolques.Parsing
{
    /// <summary>
    /// Extrae una sección concreta del evento. El parser ejecuta todos los extractores
    /// cuyo <see cref="AplicaPara"/> devuelva true, y cada uno rellena su parte.
    /// </summary>
    public interface ICameraEventSectionExtractor
    {
        public VCAModoEnum VCAModo { get; set; }

        /// <summary>
        /// Determina si este extractor debe ejecutarse.
        /// </summary>
        /// <param name="eventType">Tipo de evento ya desenvuelto (ej. "fielddetection").</param>
        /// <param name="doc">Documento normalizado, por si hace falta comprobar nodos.</param>
        bool AplicaPara(string eventType, XDocument? doc = null);

        /// <summary>Extrae su fragmento de datos y lo asigna al evento.</summary>
        void Extraer(XDocument doc, CameraEventModel evento);
    }
}
