using Newtonsoft.Json;
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

        public static string? BuscarInnerXml(this XDocument doc, params string[] xpaths) =>
            xpaths.Select(xpath => doc.XPathSelectElement(xpath)?.ToString().Trim())
                  .FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

        public static string? BuscarInnerJson(this XDocument doc, params string[] xpaths)
        {
            try
            {
                var xmlString = doc.BuscarInnerXml(xpaths);
                if (string.IsNullOrWhiteSpace(xmlString)) return null;

                var node = XElement.Parse(xmlString);
                return JsonConvert.SerializeXNode(node, Formatting.None, omitRootObject: true);
            } catch
            {
                return null;
            }
        }
    }
}
