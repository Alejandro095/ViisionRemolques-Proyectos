using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using System.Reflection;
using System.Text;
using System.Xml.Linq;

namespace ViisionRemolques.OpenApi
{
    public sealed class ControllerTagDescriptionsTransformer : IOpenApiDocumentTransformer
    {
        private const string SufijoControlador = "Controller";

        private static readonly Lazy<Dictionary<string, string>> DescripcionesPorTag =
            new(ConstruirDescripciones, LazyThreadSafetyMode.ExecutionAndPublication);

        public Task TransformAsync(
            OpenApiDocument document,
            OpenApiDocumentTransformerContext context,
            CancellationToken cancellationToken)
        {
            if (document.Tags is null || document.Tags.Count == 0)
                return Task.CompletedTask;

            var descripciones = DescripcionesPorTag.Value;

            if (descripciones.Count == 0)
                return Task.CompletedTask;

            foreach (var tag in document.Tags)
            {
                if (string.IsNullOrWhiteSpace(tag.Name))
                    continue;

                // Se respeta cualquier descripción ya establecida por otro transformador.
                if (!string.IsNullOrWhiteSpace(tag.Description))
                    continue;

                if (descripciones.TryGetValue(tag.Name, out var descripcion))
                    tag.Description = descripcion;
            }

            return Task.CompletedTask;
        }

        private static Dictionary<string, string> ConstruirDescripciones()
        {
            var resultado = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            var ensamblado = typeof(ControllerTagDescriptionsTransformer).Assembly;
            var documentacion = CargarDocumentacionXml(ensamblado);

            if (documentacion is null)
                return resultado;

            var controladores = ensamblado.GetTypes()
                .Where(t => t.IsClass
                            && !t.IsAbstract
                            && typeof(ControllerBase).IsAssignableFrom(t)
                            && t.Name.EndsWith(SufijoControlador, StringComparison.Ordinal));

            foreach (var controlador in controladores)
            {
                // El tag que genera ASP.NET para un controlador es su nombre sin el sufijo 'Controller'.
                var nombreTag = controlador.Name[..^SufijoControlador.Length];

                if (string.IsNullOrEmpty(nombreTag))
                    continue;

                var descripcion = LeerDocumentacionDeClase(documentacion, controlador);

                if (!string.IsNullOrWhiteSpace(descripcion))
                    resultado[nombreTag] = descripcion;
            }

            return resultado;
        }
        private static XDocument? CargarDocumentacionXml(Assembly ensamblado)
        {
            try
            {
                var nombre = ensamblado.GetName().Name;

                if (string.IsNullOrEmpty(nombre))
                    return null;

                var ruta = Path.Combine(AppContext.BaseDirectory, nombre + ".xml");

                return File.Exists(ruta) ? XDocument.Load(ruta) : null;
            }
            catch
            {
                // La documentación es opcional: si no se puede leer, los tags quedan sin descripción.
                return null;
            }
        }

        private static string? LeerDocumentacionDeClase(XDocument documentacion, Type tipo)
        {
            var clave = "T:" + (tipo.FullName ?? tipo.Name);

            var miembro = documentacion.Descendants("member")
                .FirstOrDefault(m => string.Equals((string?)m.Attribute("name"), clave, StringComparison.Ordinal));

            if (miembro is null)
                return null;

            var resumen = AplanarTexto(miembro.Element("summary"));
            var observaciones = AplanarTexto(miembro.Element("remarks"));

            if (string.IsNullOrWhiteSpace(resumen))
                return string.IsNullOrWhiteSpace(observaciones) ? null : observaciones;

            return string.IsNullOrWhiteSpace(observaciones)
                ? resumen
                : resumen + "\n\n" + observaciones;
        }

        private static string? AplanarTexto(XElement? elemento)
        {
            if (elemento is null)
                return null;

            var texto = new StringBuilder();

            foreach (var nodo in elemento.Nodes())
            {
                switch (nodo)
                {
                    case XText t:
                        texto.Append(t.Value);
                        break;

                    // <c>codigo</c> y <code>codigo</code> se representan como código Markdown,
                    // igual que hace el soporte nativo de comentarios XML de ASP.NET Core.
                    case XElement e when e.Name == "c" || e.Name == "code":
                        texto.Append('`').Append(e.Value.Trim()).Append('`');
                        break;

                    case XElement e when e.Name == "para":
                        texto.Append('\n').Append(e.Value).Append('\n');
                        break;

                    // <see cref="..."/> y <paramref name="..."/> se reducen al nombre referenciado.
                    case XElement e when e.Name == "see" || e.Name == "seealso":
                        texto.Append('`').Append(NombreCorto(e)).Append('`');
                        break;

                    case XElement e when e.Name == "paramref" || e.Name == "typeparamref":
                        texto.Append('`').Append((string?)e.Attribute("name")).Append('`');
                        break;

                    case XElement e:
                        texto.Append(e.Value);
                        break;
                }
            }

            return Desangrar(texto.ToString());
        }

        private static string NombreCorto(XElement elemento)
        {
            var referencia = (string?)elemento.Attribute("cref")
                             ?? (string?)elemento.Attribute("langword")
                             ?? elemento.Value;

            if (string.IsNullOrWhiteSpace(referencia))
                return string.Empty;

            // Los cref llegan como 'T:Namespace.Tipo' o 'M:Namespace.Tipo.Metodo'.
            var sinPrefijo = referencia.Length > 2 && referencia[1] == ':'
                ? referencia[2..]
                : referencia;

            var ultimoPunto = sinPrefijo.LastIndexOf('.');

            return ultimoPunto >= 0 && ultimoPunto < sinPrefijo.Length - 1
                ? sinPrefijo[(ultimoPunto + 1)..]
                : sinPrefijo;
        }

        private static string? Desangrar(string texto)
        {
            if (string.IsNullOrWhiteSpace(texto))
                return null;

            var lineas = texto.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n').ToList();

            while (lineas.Count > 0 && string.IsNullOrWhiteSpace(lineas[0]))
                lineas.RemoveAt(0);

            while (lineas.Count > 0 && string.IsNullOrWhiteSpace(lineas[^1]))
                lineas.RemoveAt(lineas.Count - 1);

            if (lineas.Count == 0)
                return null;

            var sangriaComun = lineas
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .Select(l => l.Length - l.TrimStart().Length)
                .DefaultIfEmpty(0)
                .Min();

            var normalizadas = lineas.Select(l =>
                string.IsNullOrWhiteSpace(l)
                    ? string.Empty
                    : l[Math.Min(sangriaComun, l.Length)..].TrimEnd());

            return string.Join('\n', normalizadas);
        }
    }
}
