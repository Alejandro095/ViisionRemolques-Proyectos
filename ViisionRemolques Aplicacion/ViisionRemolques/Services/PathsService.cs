using Microsoft.Extensions.Options;
using ViisionRemolques.Settings;

namespace ViisionRemolques.Services
{
    public class PathsService
    {
        private readonly PathsSettings _settings;
        private readonly IWebHostEnvironment _environment;

        public PathsService(IOptions<PathsSettings> settings, IWebHostEnvironment environment)
        {
            _settings = settings.Value;
            _environment = environment;
        }

        public string GetImagenesPath() => 
            Path.Combine(_environment.ContentRootPath, _settings.Imagenes);

        public string GetRostrosPath() => 
            Path.Combine(_environment.ContentRootPath, _settings.Rostros);
        public string GetRostrosTempPath() =>
            Path.Combine(GetRostrosPath(), "temp");
    }
}
