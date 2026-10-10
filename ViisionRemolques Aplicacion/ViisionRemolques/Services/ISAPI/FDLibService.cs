using Microsoft.Extensions.Options;
using RestSharp;
using System.Xml.Linq;
using ViisionRemolques.Repositories;
using ViisionRemolques.Settings;
using ViisionRemolques.Utils;

namespace ViisionRemolques.Services.ISAPI
{
    public class FDLibService
    {
        private readonly IsapiClientFactoryService _isapiClientFactoryService;
        private readonly FDLibSettings _fdLibSettings;

        public FDLibService(
            IsapiClientFactoryService isapiClientFactoryService,
            IOptions<FDLibSettings> fdLibSettings)
        {
            _isapiClientFactoryService = isapiClientFactoryService;
            _fdLibSettings = fdLibSettings.Value;
        }

        public async Task<Resultado<string>> CrearLibreriaAsync(
    CamaraEntity camara,
    CancellationToken ct = default)
        {
            try
            {
                using var client = _isapiClientFactoryService.Crear(camara);

                XNamespace ns = "http://www.hikvision.com/ver20/XMLSchema";
                var xmlPayload = new XDocument(
                    new XDeclaration("1.0", "UTF-8", null),
                    new XElement(ns + "CreateFDLibList",
                        new XAttribute("version", "2.0"),
                        new XElement(ns + "CreateFDLib",
                            new XElement(ns + "name", _fdLibSettings.Nombre),
                            new XElement(ns + "thresholdValue", _fdLibSettings.Threshold)
                        )
                    )
                );

                var request = new RestRequest("/ISAPI/Intelligent/FDLib", Method.Post);
                request.AddHeader("Content-Type", "application/xml");
                request.AddStringBody(xmlPayload.ToString(), DataFormat.Xml);

                var response = await client.ExecuteAsync(request, ct);

                if (!response.IsSuccessful || string.IsNullOrWhiteSpace(response.Content))
                    return Resultado<string>.Fallo($"Error al crear la biblioteca '{_fdLibSettings.Nombre}' ({camara.IP}). HTTP {response.StatusCode}. {response.Content}");

                var doc = XDocument.Parse(response.Content);
                var fdidNueva = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "FDID")?.Value;

                if (string.IsNullOrWhiteSpace(fdidNueva))
                    return Resultado<string>.Fallo("La biblioteca se creó pero la respuesta no contiene <FDID>.");

                return Resultado<string>.Ok(fdidNueva);
            }
            catch (Exception ex)
            {
                return Resultado<string>.Fallo($"Excepción al crear la biblioteca facial ({camara.IP}): {ex.ToString()}");
            }
        }

        public async Task<Resultado<string?>> IntentarObtenerFDIDLibreriaAsync(
            CamaraEntity camara, 
            CancellationToken ct = default)
        {
            try
            {
                using var client = _isapiClientFactoryService.Crear(camara);

                var response = await client.ExecuteAsync(new RestRequest(
                    "/ISAPI/Intelligent/FDLib",
                    Method.Get
                ), ct);

                if (!response.IsSuccessful || string.IsNullOrWhiteSpace(response.Content))
                    return Resultado<string?>.Fallo(
                        $"Error al listar bibliotecas faciales de la cámara ({camara.IP}). HTTP {response.StatusCode}.");

                var doc = XDocument.Parse(response.Content);

                var libreria = doc.Descendants()
                    .FirstOrDefault(e => e.Name.LocalName == "FDLibBaseCfg" &&
                        e.Elements().FirstOrDefault(c => c.Name.LocalName == "name")?.Value == _fdLibSettings.Nombre);

                if (libreria is null)
                    return Resultado<string?>.Ok(null);

                var fdid = libreria.Elements().FirstOrDefault(e => e.Name.LocalName == "FDID")?.Value;

                if (string.IsNullOrWhiteSpace(fdid))
                    return Resultado<string?>.Fallo("La biblioteca existe pero la respuesta no contiene <FDID>.");

                return Resultado<string?>.Ok(fdid);
            }
            catch (Exception ex)
            {
                return Resultado<string?>.Fallo($"Excepción al obtener el FDID de la biblioteca ({camara.IP}): {ex.ToString()}");
            }
        }

        public record InsertarImagenRequest(string Fdid, string PersonId, string Nombre, byte[] Imagen);

        public async Task<Resultado<string>> InsertarImagenAsync(
            CamaraEntity camara,
            InsertarImagenRequest insertarImagenRequest,
            CancellationToken ct = default)
        {
            try
            {
                using var client = _isapiClientFactoryService.Crear(camara);

                XNamespace ns = "http://www.hikvision.com/ver20/XMLSchema";
                var xmlPayload = new XDocument(
                    new XDeclaration("1.0", "UTF-8", null),
                    new XElement(ns + "PictureUploadData",
                        new XAttribute("version", "2.0"),
                        new XElement(ns + "FDID", insertarImagenRequest.Fdid),
                        new XElement(ns + "FaceAppendData",
                            new XElement(ns + "name", insertarImagenRequest.Nombre),
                            new XElement(ns + "certificateType", "officerID"),
                            new XElement(ns + "certificateNumber", insertarImagenRequest.PersonId),
                            new XElement(ns + "customHumanID", insertarImagenRequest.PersonId)
                        )
                    )
                );

                var boundary = "----FormBoundary" + Guid.NewGuid().ToString("N")[..16];
                using var body = new MemoryStream();
                void Write(string s) => body.Write(System.Text.Encoding.UTF8.GetBytes(s));

                Write($"--{boundary}\r\nContent-Disposition: form-data; name=\"PictureUploadData\"\r\n\r\n");
                Write(xmlPayload.ToString());
                Write($"\r\n--{boundary}\r\nContent-Disposition: form-data; name=\"importImage\"; filename=\"{insertarImagenRequest.PersonId}.jpg\"\r\nContent-Type: image/jpeg\r\n\r\n");
                body.Write(insertarImagenRequest.Imagen);
                Write($"\r\n--{boundary}--\r\n");

                var request = new RestRequest("/ISAPI/Intelligent/FDLib/pictureUpload?type=concurrent", Method.Post);
                request.AddParameter($"multipart/form-data; boundary={boundary}", body.ToArray(), ParameterType.RequestBody);

                var response = await client.ExecuteAsync(request, ct);

                if (!response.IsSuccessful || string.IsNullOrWhiteSpace(response.Content))
                    return Resultado<string>.Fallo(
                         $"Error al subir foto de PersonId {insertarImagenRequest.PersonId} ({camara.IP}). HTTP {response.StatusCode}. {response.Content}");

                var doc = XDocument.Parse(response.Content);
                var pid = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "PID")?.Value;

                if (string.IsNullOrWhiteSpace(pid))
                    return Resultado<string>.Fallo($"La foto se subió pero la respuesta no contiene <PID> (PersonId {insertarImagenRequest.PersonId}).");

                return Resultado<string>.Ok(pid);
            }
            catch (Exception ex)
            {
                return Resultado<string>.Fallo($"Excepción al subir foto de PersonId {insertarImagenRequest.PersonId} ({camara.IP}): {ex.ToString()}");
            }
        }


        public async Task<Resultado> ModelarPendientesAsync(
            CamaraEntity camara, 
            string fdid, 
            CancellationToken ct = default)
        {
            try
            {
                using var client = _isapiClientFactoryService.Crear(camara);

                var request = new RestRequest(
                    $"/ISAPI/Intelligent/FDLib/manualModeling?range=unmodeled&FDID={fdid}",
                    Method.Get
                );

                var response = await client.ExecuteAsync(request, ct);

                if (!response.IsSuccessful)
                    return Resultado.Fallo(
                        $"Error al disparar modelado por tandas de la biblioteca {fdid} ({camara.IP}). HTTP {response.StatusCode}.");

                return Resultado.Ok();
            }
            catch (Exception ex)
            {
                return Resultado.Fallo($"Excepción al disparar modelado por tandas de la biblioteca {fdid} ({camara.IP}): {ex.ToString()}");
            }
        }

        public async Task<Resultado> BorrarImagenAsync(
            CamaraEntity camara, 
            string fdid, 
            string pid, 
            CancellationToken ct = default)
        {
            try
            {
                using var client = _isapiClientFactoryService.Crear(camara);

                var request = new RestRequest($"/ISAPI/Intelligent/FDLib/{fdid}/picture/{pid}", Method.Delete);

                var response = await client.ExecuteAsync(request, ct);

                // Cuando el pid no existe responde con un 500
                if (response.StatusCode == System.Net.HttpStatusCode.InternalServerError)
                    return Resultado.Ok();

                if (!response.IsSuccessful)
                    return Resultado.Fallo(
                        $"Error al borrar PID {pid} de la biblioteca {fdid} ({camara.IP}). HTTP {response.StatusCode}.");

                return Resultado.Ok();
            }
            catch (Exception ex)
            {
                return Resultado.Fallo($"Excepción al borrar PID {pid} de la biblioteca {fdid} ({camara.IP}): {ex.ToString()}");
            }
        }

        public record RegistroFacial(string Pid, string Nombre, string CustomHumanID, string Estado);

        public async Task<Resultado<IEnumerable<RegistroFacial>>> ConsultarRegistrosAsync(
            CamaraEntity camara, 
            string fdid, 
            CancellationToken ct = default)
        {
            try
            {
                using var client = _isapiClientFactoryService.Crear(camara);

                var resultado = new List<RegistroFacial>();

                foreach (var status in new[] { "success", "failed", "none" })
                {
                    XNamespace ns = "http://www.isapi.org/ver20/XMLSchema";
                    var xmlPayload = new XDocument(
                        new XDeclaration("1.0", "UTF-8", null),
                        new XElement(ns + "FDModelingStatusSearchDescription",
                            new XAttribute("version", "2.0"),
                            new XElement(ns + "searchID", Guid.NewGuid().ToString()),
                            new XElement(ns + "searchResultPosition", 1),
                            new XElement(ns + "maxResults", 500),
                            new XElement(ns + "CondList",
                                new XElement(ns + "Cond",
                                    new XElement(ns + "FDID", fdid)
                                )
                            )
                        )
                    );

                    var request = new RestRequest($"/ISAPI/Intelligent/FDLib/modelingStatus?status={status}", Method.Post);
                    request.AddHeader("Content-Type", "application/xml");
                    request.AddStringBody(xmlPayload.ToString(), DataFormat.Xml);

                    var response = await client.ExecuteAsync(request, ct);

                    if (!response.IsSuccessful || string.IsNullOrWhiteSpace(response.Content))
                        return Resultado<IEnumerable<RegistroFacial>>.Fallo(
                            $"Error al consultar registros con estado '{status}' de la biblioteca {fdid} ({camara.IP}). HTTP {response.StatusCode}.");

                    var doc = XDocument.Parse(response.Content);

                    resultado.AddRange(doc.Descendants()
                        .Where(e => e.Name.LocalName == "ModelingStatus")
                        .Select(e => new RegistroFacial(
                            Pid: e.Elements().FirstOrDefault(c => c.Name.LocalName == "PID")?.Value ?? "",
                            Nombre: e.Elements().FirstOrDefault(c => c.Name.LocalName == "name")?.Value ?? "",
                            CustomHumanID: e.Elements().FirstOrDefault(c => c.Name.LocalName == "customHumanID")?.Value ?? "",
                            Estado: status
                        )));
                }

                return Resultado<IEnumerable<RegistroFacial>>.Ok(resultado);
            }
            catch (Exception ex)
            {
                return Resultado<IEnumerable<RegistroFacial>>.Fallo($"Excepción al consultar registros de la biblioteca {fdid} ({camara.IP}): {ex.ToString()}");
            }
        }
    }
}
