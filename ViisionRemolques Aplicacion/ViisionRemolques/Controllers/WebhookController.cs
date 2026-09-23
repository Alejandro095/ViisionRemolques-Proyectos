using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Net.Http.Headers;
using System.Net.Http.Headers;
using ViisionRemolques.Entities;
using ViisionRemolques.Repositories;
using ViisionRemolques.Parsing;
using ViisionRemolques.Parsing.Models;

namespace ViisionRemolques.Controllers
{
    [ApiController]
    [Route("webhook")]
    public class WebhookController : ControllerBase
    {
        private readonly AlarmaDesconocidaLogRepository _alarmaDesconocidaLogRepository;
        private readonly EventoPerimetralRepository _repo;
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<WebhookController> _logger;

        public WebhookController(
            EventoPerimetralRepository repo,
            IWebHostEnvironment env,
            ILogger<WebhookController> logger,
            AlarmaDesconocidaLogRepository alarmasDesconocidasLogRepository)
        {
            _repo = repo;
            _env = env;
            _logger = logger;
            _alarmaDesconocidaLogRepository = alarmasDesconocidasLogRepository;
        }

        [HttpPost]
        [DisableRequestSizeLimit]
        public async Task<IActionResult> Post()
        {
            try
            {
                string? cuerpo = null;
                var imagenes = new List<byte[]>();

                if (Request.HasFormContentType)
                {
                    var form = await Request.ReadFormAsync();

                    foreach (var campo in form)
                        if (!string.IsNullOrWhiteSpace(campo.Value))
                            cuerpo ??= campo.Value.ToString();

                    foreach (var fichero in form.Files)
                    {
                        if (fichero.ContentType?.StartsWith("image/", StringComparison.OrdinalIgnoreCase) == true)
                        {
                            using var ms = new MemoryStream();
                            await fichero.CopyToAsync(ms);
                            imagenes.Add(ms.ToArray());
                        }
                        else
                        {
                            using var sr = new StreamReader(fichero.OpenReadStream());
                            cuerpo ??= await sr.ReadToEndAsync();
                        }
                    }
                }
                else
                {
                    cuerpo = await new StreamReader(Request.Body).ReadToEndAsync();
                }


                CameraEventModel? evento = CameraEventParser.Parse(cuerpo);

                string? pathImagen = null;

                if (imagenes.Count > 0)
                {
                    var carpeta = Path.Combine(_env.ContentRootPath, "AlertasPerimetrales", DateTime.UtcNow.ToString("yyyy-MM-dd"));
                    Directory.CreateDirectory(carpeta);

                    var nombreArchivo = $"{evento.BaseInfo.PId ?? Guid.NewGuid().ToString("N")}.jpg";
                    pathImagen = Path.Combine(carpeta, nombreArchivo);

                    await System.IO.File.WriteAllBytesAsync(pathImagen, imagenes[0]);
                }

                if (evento is not null && evento.BaseInfo.EventType == "heartBeat" && evento.BaseInfo.EventType == "VMD")
                {
                    return Ok();
                }

                if (evento is null || evento.BaseInfo.VCAModo == Enums.VCAModoEnum.Ninguno)
                {
                    await _alarmaDesconocidaLogRepository.InsertarAsync(new AlarmaDesonocidaLogEntity()
                    {
                        IPCamara = HttpContext.Connection.RemoteIpAddress?.ToString(),
                        ContentType = Request.ContentType,
                        Evento = evento?.BaseInfo.EventType ?? null,
                        Body = cuerpo,
                        Motivo = evento is null ? "PARSEO_ERROR" : "EXTRACTOR_DETALLES_ERROR"
                    });

                    return Ok();
                }


                if (evento.BaseInfo.VCAModo == Enums.VCAModoEnum.EventoSmart && evento.EventoSmart.RegionID is null)
                {
                    var hola = "hola mundo";
                }

                switch (evento.BaseInfo.VCAModo)
                {
                    case Enums.VCAModoEnum.EventoSmart:

                        await _repo.InsertarAsync(new EventoPerimetral()
                        {
                            PId = evento.BaseInfo.PId,
                            IPCamara = evento.BaseInfo.IpAddress,
                            Evento = evento.BaseInfo.EventType,
                            ReglaId = evento.EventoSmart.RegionID,
                            TipoObjetivo = evento.EventoSmart.DetectionTarget,
                            FechaEvento = DateTime.Now,
                            PathImagen = pathImagen
                        });
                        break;
                }

                return Ok();
            } catch (Exception exception)
            {
                _logger.LogCritical(exception.ToString());

                return Ok();
            }
        }
    }
}

























//using Microsoft.AspNetCore.Mvc;
//using ViisionRemolques.Repositories;
//using ViisionRemolques.Utils;
//using ViisionRemolques.Utils.Modelos;

//namespace ViisionRemolques.Controllers
//{
//    [ApiController]
//    [Route("webhook")]
//    public class WebhookController : ControllerBase
//    {

//        private readonly EventoPerimetralRepository _repo;
//        private readonly IWebHostEnvironment _env;
//        private readonly ILogger<WebhookController> _logger;

//        public WebhookController(
//            EventoPerimetralRepository repo,
//            IWebHostEnvironment env,
//            ILogger<WebhookController> logger)
//        {
//            _repo = repo;
//            _env = env;
//            _logger = logger;
//        }

//        [HttpPost]
//        [DisableRequestSizeLimit]
//        public async Task<IActionResult> Post()
//        {

//            string? cuerpo = null;
//            byte[]? imageBytes = null;

//            var contentType = Request.ContentType ?? "";

//            if (contentType.Contains("multipart/form-data"))
//            {
//                var boundary = MediaTypeHeaderValue.Parse(contentType).Boundary.Value!;
//                var reader = new MultipartReader(boundary, Request.Body);

//                MultipartSection? section;
//                while ((section = await reader.ReadNextSectionAsync()) != null)
//                {
//                    var cd = section.GetContentDispositionHeader();
//                    if (cd is null) continue;

//                    if (cd.IsFileDisposition())
//                    {
//                        using var ms = new MemoryStream();
//                        await section.Body.CopyToAsync(ms);
//                        imageBytes = ms.ToArray();
//                    }
//                    else
//                    {
//                        cuerpo = await new StreamReader(section.Body).ReadToEndAsync();
//                    }
//                }
//            }
//            else
//            {
//                // Sin filtrar por Content-Type: estas cámaras lo declaran mal, y un
//                // cuerpo descartado aquí se perdería en silencio. El formato real lo
//                // decide el parser mirando el contenido.
//                cuerpo = await new StreamReader(Request.Body).ReadToEndAsync();
//            }

//            if (string.IsNullOrWhiteSpace(cuerpo))
//                return Ok();

//            //var camEvt = CameraEventParser.Parse(cuerpo, _logger);

//            // Se compara contra el tipo crudo: EventType ya viene desenvuelto y para un
//            // "duration" trae el evento real (fielddetection), nunca la palabra duration.
//            if (EventosIgnorados.Contains(camEvt.RawEventType, StringComparer.OrdinalIgnoreCase))
//                return Ok();

//            string? pathImagen = null;

//            if (imageBytes is not null)
//            {
//                var carpeta = Path.Combine(_env.ContentRootPath, "AlertasPerimetrales", DateTime.UtcNow.ToString("yyyy-MM-dd"));
//                Directory.CreateDirectory(carpeta);

//                var nombreArchivo = $"{camEvt.PId ?? Guid.NewGuid().ToString("N")}.jpg";
//                pathImagen = Path.Combine(carpeta, nombreArchivo);

//                await System.IO.File.WriteAllBytesAsync(pathImagen, imageBytes);
//            }

//            var evento = new EventoPerimetral
//            {
//                PId = camEvt.PId,
//                IPCamara = camEvt.IpAddress,
//                Evento = camEvt.EventType,
//                ReglaId = camEvt.RegionID,
//                TipoObjetivo = camEvt.DetectionTarget,
//                FechaEvento = DateTime.UtcNow,
//                PathImagen = pathImagen,
//                IdExterno = camEvt.TargetID,
//                Sincronizado = false
//            };

//            var id = await _repo.InsertarAsync(evento);

//            return Ok(new { id, pathImagen });
//        }
//    }
//}