using Microsoft.Extensions.Options;
using RestSharp;
using System.Text.Json.Serialization;
using ViisionRemolques.Settings;

namespace ViisionRemolques.Services.Centralia
{
    public class CentraliaApiClientService : IDisposable
    {
        private readonly CentraliaSettings _centraliaSettings;
        private readonly RestClient _client;
        public readonly string Licencia = "8F^3k1z#0@pLm!2Wv#4Xs7z$8qRTy*1B";
        private string _tokenActual = string.Empty;
        private DateTime _fechaExpiracionToken = DateTime.MinValue;
        private readonly SemaphoreSlim _semaforoToken = new(1, 1);
        private string _usuarioActual = string.Empty;
        private string _contraseniaActual = string.Empty;

        public CentraliaApiClientService(HttpClient httpClient, IOptions<CentraliaSettings> centraliaSettings)
        {
            _centraliaSettings = centraliaSettings.Value;
            var options = new RestClientOptions(httpClient.BaseAddress ?? new Uri("https://localhost:7134"));
            _client = new RestClient(httpClient, options);
        }

        public async Task IniciarSesionAsync(string usuario, string contrasenia)
        {
            _usuarioActual = usuario;
            _contraseniaActual = contrasenia;
            await RenovarTokenAsync();
        }

        public async Task<TResponse> PostAsync<TResponse>(string endpoint, IRequestConLicencia payload)
        {
            await AsegurarTokenValidoAsync();

            var url = _centraliaSettings.GetApiFullUrl(endpoint);
            var request = new RestRequest(url, Method.Post);

            request.AddHeader("Authorization", $"Bearer {_tokenActual}");

            // Asignamos la licencia al contrato
            payload.Licencia = this.Licencia;

            request.AddJsonBody(payload);

            var response = await _client.ExecuteAsync<ApiResponse<TResponse>>(request);

            if (response.IsSuccessful && response.Data != null && !response.Data.Error)
            {
                // Validación para evitar que C# advierta sobre el retorno nulo
                if (response.Data.Data == null)
                    throw new Exception($"La API reportó éxito en {endpoint}, pero el objeto 'data' vino nulo.");

                return response.Data.Data;
            }

            throw new Exception($"Error en {endpoint}: {response.Data?.Message ?? response.ErrorMessage}");
        }

        private async Task AsegurarTokenValidoAsync()
        {
            if (!string.IsNullOrEmpty(_tokenActual) && DateTime.UtcNow < _fechaExpiracionToken.AddMinutes(-1))
                return;

            await _semaforoToken.WaitAsync();
            try
            {
                if (!string.IsNullOrEmpty(_tokenActual) && DateTime.UtcNow < _fechaExpiracionToken.AddMinutes(-1))
                    return;

                await RenovarTokenAsync();
            }
            finally
            {
                _semaforoToken.Release();
            }
        }

        private async Task RenovarTokenAsync()
        {
            if (string.IsNullOrEmpty(_usuarioActual) || string.IsNullOrEmpty(_contraseniaActual))
                throw new InvalidOperationException("Credenciales no configuradas. Llama a IniciarSesionAsync().");

            var url = _centraliaSettings.GetApiFullUrl("Auth/login");
            var request = new RestRequest(url, Method.Post);

            request.AddJsonBody(new
            {
                usuario = _usuarioActual,
                contrasenia = _contraseniaActual,
                idCatUsuarioTipo = 0
            });

            var response = await _client.ExecuteAsync<ApiResponse<LoginDataResponse>>(request);

            if (response.IsSuccessful && response.Data != null && !response.Data.Error)
            {
                // Navegación segura para extraer el token
                _tokenActual = response.Data.Data?.Token ?? string.Empty;

                if (string.IsNullOrEmpty(_tokenActual))
                    throw new Exception("El login fue exitoso pero el JSON no contenía un token válido.");

                _fechaExpiracionToken = DateTime.UtcNow.AddMinutes(15);
                return;
            }

            throw new Exception($"Falló la renovación del token. Status: {response.StatusCode} - {response.Data?.Message}");
        }

        public void Dispose()
        {
            _client?.Dispose();
            _semaforoToken?.Dispose();
        }
    }

    public interface IRequestConLicencia
    {
        string Licencia { get; set; }
    }

    public class ApiResponse<T>
    {
        [JsonPropertyName("error")]
        public bool Error { get; set; }

        [JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;

        [JsonPropertyName("data")]
        public T? Data { get; set; }
    }

    public class LoginDataResponse
    {
        [JsonPropertyName("token")]
        public string? Token { get; set; }
    }
}