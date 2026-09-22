using System.Globalization;
using System.Xml.Linq;
using System.Xml.XPath;

namespace ViisionRemolques.Parsing
{
    public static class XmlQueryExtensions
    {
        public static string? Buscar(this XDocument doc, params string[] xpaths) =>
            xpaths.Select(xpath => doc.XPathSelectElement(xpath)?.Value.Trim())
                  .FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
    }
}
