using Microsoft.Extensions.Options;
using RestSharp;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using ViisionRemolques.Settings;

namespace ViisionRemolques.Services.Centralia
{
    public class CentraliaApiClientService : IDisposable
    {
        private readonly CentraliaSettings _centraliaSettings;
        private readonly RestClient _client;
        private string _tokenActual = string.Empty;
        private DateTime _fechaExpiracionToken = DateTime.MinValue;
        private readonly SemaphoreSlim _semaforoToken = new(1, 1);

        public CentraliaApiClientService(HttpClient httpClient, IOptions<CentraliaSettings> centraliaSettings)
        {
            _centraliaSettings = centraliaSettings.Value;
            var options = new RestClientOptions(httpClient.BaseAddress ?? new Uri("https://localhost:7134"));
            _client = new RestClient(httpClient, options);
        }

        public async Task<TResponse> GetAsync<TResponse>(string endpoint)
        {
            return await SendAsync<TResponse>(endpoint, Method.Get, null);
        }

        public async Task<TResponse> PostAsync<TResponse>(string endpoint, object? payload = null)
        {
            return await SendAsync<TResponse>(endpoint, Method.Post, payload);
        }

        public async Task<TResponse> PutAsync<TResponse>(string endpoint, object? payload = null)
        {
            return await SendAsync<TResponse>(endpoint, Method.Put, payload);
        }

        public async Task<TResponse> DeleteAsync<TResponse>(string endpoint)
        {
            return await SendAsync<TResponse>(endpoint, Method.Delete, null);
        }

        private async Task<TResponse> SendAsync<TResponse>(string endpoint, Method method, object? payload)
        {
            await AsegurarTokenValidoAsync();

            var url = _centraliaSettings.GetApiFullUrl(endpoint);
            var request = new RestRequest(url, method);

            request.AddHeader("Authorization", $"Bearer {_tokenActual}");

            if (method == Method.Post || method == Method.Put || method == Method.Patch)
            {
                request.AddHeader("Content-Type", "application/json");

                var jsonNode = JsonSerializer.SerializeToNode(payload)?.AsObject() ?? new JsonObject();
                jsonNode["licencia"] = _centraliaSettings.Licencia;

                request.AddStringBody(jsonNode.ToString(), DataFormat.Json);
            }

            var response = await _client.ExecuteAsync<ApiResponse<TResponse>>(request);

            if (response.IsSuccessful && response.Data != null && !response.Data.Error)
            {
                if (response.Data.Data == null)
                    throw new Exception($"La API reportó éxito en {endpoint}, pero el objeto 'data' vino nulo.");

                return response.Data.Data;
            }

            throw new Exception($"Error en {endpoint} ({method}): {response.Data?.Message ?? response.ErrorMessage}");
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
            if (string.IsNullOrEmpty(_centraliaSettings.Usuario) || string.IsNullOrEmpty(_centraliaSettings.Secreto))
                throw new InvalidOperationException("Credenciales no configuradas en CentraliaSettings.");

            var url = _centraliaSettings.GetApiFullUrl("Auth/login");
            var request = new RestRequest(url, Method.Post);

            request.AddJsonBody(new
            {
                usuario = _centraliaSettings.Usuario,
                contrasenia = _centraliaSettings.Secreto,
                idCatUsuarioTipo = 0
            });

            var response = await _client.ExecuteAsync<ApiResponse<LoginDataResponse>>(request);

            if (response.IsSuccessful && response.Data != null && !response.Data.Error)
            {
                _tokenActual = response.Data.Data?.Token ?? string.Empty;

                if (string.IsNullOrEmpty(_tokenActual))
                    throw new Exception("El login fue exitoso pero el JSON no contenía un token válido.");

                _fechaExpiracionToken = DateTime.UtcNow.AddMinutes(15);
                return;
            }

            throw new Exception($"Falló la renovación automática del token. Status: {response.StatusCode} - {response.Data?.Message}");
        }

        public void Dispose()
        {
            _client?.Dispose();
            _semaforoToken?.Dispose();
        }
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