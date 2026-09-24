namespace ViisionRemolques.Services
{
    public class WebhookPayloadModel
    {
        public string? Body { get; set; }
        public List<byte[]> Imagenes { get; set; } = new();
    }
    public class WebhookPayloadExtractorService
    {
        public async Task<WebhookPayloadModel> Extraer(HttpRequest request)
        {
            var resultado = new WebhookPayloadModel();

            if (request.HasFormContentType)
            {
                var form = await request.ReadFormAsync();

                foreach (var campo in form)
                {
                    if (!string.IsNullOrWhiteSpace(campo.Value))
                    {
                        resultado.Body ??= campo.Value.ToString();
                    }
                }

                foreach (var fichero in form.Files)
                {
                    if (fichero.ContentType?.StartsWith("image/", StringComparison.OrdinalIgnoreCase) == true)
                    {
                        using var ms = new MemoryStream();
                        await fichero.CopyToAsync(ms);
                        resultado.Imagenes.Add(ms.ToArray());
                    }
                    else
                    {
                        using var sr = new StreamReader(fichero.OpenReadStream());
                        resultado.Body ??= await sr.ReadToEndAsync();
                    }
                }
            }
            else
            {
                using var sr = new StreamReader(request.Body);
                resultado.Body = await sr.ReadToEndAsync();
            }

            return resultado;
        }
    }
}
