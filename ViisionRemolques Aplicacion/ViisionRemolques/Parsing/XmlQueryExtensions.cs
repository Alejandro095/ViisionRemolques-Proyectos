using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
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


        public static string? BuscarInnerArrayJson(this XDocument doc, params string[] xpaths)
        {
            try
            {
                // Buscamos el nodo contenedor (p. ej. <RegionList>)
                var node = xpaths.Select(xpath => doc.XPathSelectElement(xpath))
                                 .FirstOrDefault(n => n != null);

                if (node == null) return null;

                // Si el nodo contenedor no tiene hijos con elementos, retornamos array vacío
                if (!node.HasElements) return "[]";

                // Obtenemos el JSON serializando todo el nodo contenedor
                var jsonString = JsonConvert.SerializeXNode(node, Formatting.None, omitRootObject: true);
                var token = JToken.Parse(jsonString);

                // Caso 1: <RegionList> tenía múltiples elementos y Newtonsoft creó una propiedad con un JArray
                if (token is JObject obj && obj.Properties().FirstOrDefault()?.Value is JArray jArray)
                {
                    return jArray.ToString(Formatting.None);
                }

                // Caso 2: <RegionList> tenía solo 1 elemento y Newtonsoft creó un JObject simple
                if (token is JObject singleObj && singleObj.Properties().FirstOrDefault()?.Value is JObject childObj)
                {
                    return new JArray(childObj).ToString(Formatting.None);
                }

                // Caso 3: Si por alguna razón el token en sí ya es un JArray
                if (token is JArray arr)
                {
                    return arr.ToString(Formatting.None);
                }

                // Respaldo: Si solo era un único objeto JSON plano, lo envolvemos en array
                return new JArray(token).ToString(Formatting.None);
            }
            catch
            {
                return null;
            }
        }




    }
}
