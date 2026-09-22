using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Net.Http.Headers;
using System.Xml.Linq;
using System.Xml.Serialization;
using ViisionRemolques.Entities;
using ViisionRemolques.Parsing;
using ViisionRemolques.Repositories;

namespace ViisionRemolques.Controllers
{
    [ApiController]
    [Route("webhook")]
    public class WebhookController : ControllerBase
    {
        private readonly EventoPerimetralRepository _repo;
        private readonly IWebHostEnvironment _env;

        public WebhookController(EventoPerimetralRepository repo, IWebHostEnvironment env)
        {
            _repo = repo;
            _env = env;
        }

        [HttpPost]
        [DisableRequestSizeLimit]
        public async Task<IActionResult> Post()
        {
            string? xml = null;
            byte[]? imageBytes = null;

            var contentType = Request.ContentType ?? "";

            if (contentType.Contains("multipart/form-data"))
            {
                var boundary = MediaTypeHeaderValue.Parse(contentType).Boundary.Value!;
                var reader = new MultipartReader(boundary, Request.Body);

                MultipartSection? section;
                while ((section = await reader.ReadNextSectionAsync()) != null)
                {
                    var cd = section.GetContentDispositionHeader();
                    if (cd is null) continue;

                    if (cd.IsFileDisposition())
                    {
                        using var ms = new MemoryStream();
                        await section.Body.CopyToAsync(ms);
                        imageBytes = ms.ToArray();
                    }
                    else
                    {
                        xml = await new StreamReader(section.Body).ReadToEndAsync();
                    }
                }
            }
            else if (contentType.Contains("xml"))
            {
                xml = await new StreamReader(Request.Body).ReadToEndAsync();
            }

            if (string.IsNullOrWhiteSpace(xml))
                return Ok();

            var camEvt = CameraEventParser.Parse(xml);

            if (camEvt.EventType == "heartBeat")
                return Ok();

            string? pathImagen = null;

            if (imageBytes is not null)
            {
                var carpeta = Path.Combine(_env.ContentRootPath, "AlertasPerimetrales", DateTime.UtcNow.ToString("yyyy-MM-dd"));
                Directory.CreateDirectory(carpeta);

                var nombreArchivo = $"{camEvt.PId ?? Guid.NewGuid().ToString("N")}.jpg";
                pathImagen = Path.Combine(carpeta, nombreArchivo);

                await System.IO.File.WriteAllBytesAsync(pathImagen, imageBytes);
            }

            var evento = new EventoPerimetral
            {
                PId = camEvt.PId,
                IPCamara = camEvt.IpAddress,
                Evento = camEvt.EventType,
                ReglaId = camEvt.DetectionRegionList?.FirstOrDefault()?.RegionID?.ToString(),
                TipoObjetivo = camEvt.DetectionRegionList?.FirstOrDefault()?.DetectionTarget,
                FechaEvento = DateTime.UtcNow,
                PathImagen = pathImagen,
                IdExterno = camEvt.TargetID,
                Sincronizado = false
            };

            var id = await _repo.InsertarAsync(evento);

            return Ok(new { id, pathImagen });
        }
    }
}

[XmlRoot("EventNotificationAlert")]
public class CameraEvent
{
    [XmlElement("ipAddress")]
    public string? IpAddress { get; set; }

    [XmlElement("channelID")]
    public int? ChannelID { get; set; }

    [XmlElement("dateTime")]
    public string? DateTime { get; set; }

    [XmlElement("eventType")]
    public string? EventType { get; set; }

    [XmlElement("eventState")]
    public string? EventState { get; set; }

    [XmlElement("pId")]
    public string? PId { get; set; }

    [XmlElement("targetID")]
    public int? TargetID { get; set; }

    [XmlElement("timeStamp")]
    public long? TimeStamp { get; set; }

    [XmlArray("DetectionRegionList")]
    [XmlArrayItem("DetectionRegionEntry")]
    public List<DetectionRegionEntry>? DetectionRegionList { get; set; }

    [XmlAnyElement]
    public System.Xml.XmlElement[]? Extra { get; set; }
}

public class DetectionRegionEntry
{
    [XmlElement("regionID")]
    public int? RegionID { get; set; }

    [XmlElement("sensitivityLevel")]
    public int? SensitivityLevel { get; set; }

    [XmlElement("detectionTarget")]
    public string? DetectionTarget { get; set; }
}

namespace ViisionRemolques.Parsing
{
    public static class CameraEventParser
    {
        public static CameraEvent Parse(string rawXml)
        {
            var doc = XDocument.Parse(rawXml);

            foreach (var el in doc.Descendants())
            {
                el.Name = el.Name.LocalName;
                el.ReplaceAttributes(
                    el.Attributes().Where(a => !a.IsNamespaceDeclaration)
                      .Select(a => new XAttribute(a.Name.LocalName, a.Value)));
            }

            var serializer = new XmlSerializer(typeof(CameraEvent));
            using var reader = doc.CreateReader();
            return (CameraEvent)serializer.Deserialize(reader)!;
        }
    }
}