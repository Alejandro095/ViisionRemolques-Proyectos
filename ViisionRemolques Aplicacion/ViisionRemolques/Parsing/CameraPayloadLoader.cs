using System.Text.Json;
using System.Xml;
using System.Xml.Linq;

namespace ViisionRemolques.Parsing
{
    public static class CameraPayloadLoader
    {
        private const string RaizPorDefecto = "eventnotificationalert";

        public static XDocument? ToNormalizedXml(string? cuerpo)
        {
            if (string.IsNullOrWhiteSpace(cuerpo)) return null;

            var texto = cuerpo.TrimStart('﻿', ' ', '\t', '\r', '\n');

            var doc = EsJson(texto) ? CargarJson(texto) : CargarXml(texto);

            if (doc is null) return null;

            Normalizar(doc);

            return doc;
        }

        private static bool EsJson(string texto) =>
            texto.StartsWith('{') || texto.StartsWith('[');

        private static XDocument? CargarXml(string texto)
        {
            try
            {
                return XDocument.Parse(texto);
            }
            catch (XmlException)
            {
                return null;
            }
        }

        private static XDocument? CargarJson(string texto)
        {
            try
            {
                using var json = JsonDocument.Parse(texto);

                var raiz = json.RootElement;

                if (raiz.ValueKind == JsonValueKind.Array)
                {
                    // Se asume un evento por petición. Si llega más de uno esa suposición
                    // era falsa, y vale más no interpretar nada —con el aviso que ya emite
                    // el controller— que descartar eventos en silencio devolviendo 200 OK.
                    if (raiz.GetArrayLength() != 1) return null;

                    raiz = raiz[0];
                }

                return new XDocument(ConvertirElemento(raiz, RaizPorDefecto));
            }
            catch (JsonException)
            {
                return null;
            }
        }

        private static void Normalizar(XDocument doc)
        {
            foreach (var el in doc.Descendants())
            {
                el.Name = el.Name.LocalName.ToLowerInvariant();

                el.ReplaceAttributes(
                    el.Attributes()
                      .Where(a => !a.IsNamespaceDeclaration)
                      .Select(a => new XAttribute(a.Name.LocalName, a.Value)));
            }
        }

        private static XElement ConvertirElemento(JsonElement json, string nombre)
        {
            var el = new XElement(NombreValido(nombre));

            switch (json.ValueKind)
            {
                case JsonValueKind.Object:
                    foreach (var prop in json.EnumerateObject())
                    {
                        if (prop.Value.ValueKind == JsonValueKind.Array)
                            foreach (var item in prop.Value.EnumerateArray())
                                el.Add(ConvertirElemento(item, prop.Name));
                        else
                            el.Add(ConvertirElemento(prop.Value, prop.Name));
                    }
                    break;

                case JsonValueKind.Array:
                    foreach (var item in json.EnumerateArray())
                        el.Add(ConvertirElemento(item, nombre));
                    break;

                case JsonValueKind.String:
                    el.Value = json.GetString() ?? string.Empty;
                    break;

                case JsonValueKind.Number:
                    el.Value = json.GetRawText();
                    break;

                case JsonValueKind.True:
                    el.Value = "true";
                    break;

                case JsonValueKind.False:
                    el.Value = "false";
                    break;
            }

            return el;
        }
        
        private static string NombreValido(string clave)
        {
            var limpio = new string(clave
                .Where(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '-' or '.')
                .ToArray())
                .ToLowerInvariant();

            return limpio.Length == 0 || !char.IsAsciiLetter(limpio[0]) ? "_" + limpio : limpio;
        }
    }
}
