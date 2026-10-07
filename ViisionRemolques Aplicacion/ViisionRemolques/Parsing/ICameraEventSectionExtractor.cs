using System.Xml.Linq;
using ViisionRemolques.Enums;
using ViisionRemolques.Parsing.Models;

namespace ViisionRemolques.Parsing
{
    public interface ICameraEventSectionExtractor
    {
        public VCAModoEnum VCAModo { get; set; }

        bool AplicaPara(string eventType, XDocument? doc = null);
        void Extraer(XDocument doc, EventoExtractorModelo evento);
    }
}
