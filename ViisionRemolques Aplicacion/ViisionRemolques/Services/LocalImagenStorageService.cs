namespace ViisionRemolques.Services
{
    public class LocalImagenStorageService
    {

        private readonly string _basePath;

        public LocalImagenStorageService(IWebHostEnvironment environment) {

            _basePath = Path.Combine(environment.ContentRootPath, "Storage/Imagenes");
        }


        //public async Task<string?> GuardarImagenesAsync(List<byte[]> imagenes, string fileExtension = ".jpg")
        //{
        //    //if (imagenes.Count == 0) return null;

        //    //if (!fileExtension.StartsWith("."))
        //    //{
        //    //    fileExtension = "." + fileExtension;
        //    //}




        //}
    }
}
