using RestSharp;
using ViisionRemolques.Entities;
using ViisionRemolques.Enums;
using ViisionRemolques.Utils;

namespace ViisionRemolques.Services.ISAPI.VCA
{
    public class VcaService
    {
        private readonly ISAPIClientFactoryService _isapiClientFactoryService;
        private readonly VcaProviderResolver _vcaProviderResolver;

        public VcaService(VcaProviderResolver vcaProviderResolver, ISAPIClientFactoryService isapiClientFactoryService)
        {
            _vcaProviderResolver = vcaProviderResolver;
            _isapiClientFactoryService = isapiClientFactoryService;
        }

        public async Task<Resultado<IEnumerable<VCAModoEnum>>> ObtenerModosSoportadosAsync(Camara camara, CancellationToken ct)
        {
            var provider = _vcaProviderResolver.Resolver(camara);
            if (provider is null)
                return Resultado<IEnumerable<VCAModoEnum>>.Fallo($"La cámara ({camara.IP}) no soporta VCA.");

            return await provider.ObtenerModosSoportados(camara, ct);
        }

        public async Task<Resultado<VCAModoEnum>> ObtenerModoActualAsync(Camara camara, CancellationToken ct)
        {
            var provider = _vcaProviderResolver.Resolver(camara);
            if (provider is null)
                return Resultado<VCAModoEnum>.Fallo($"La cámara ({camara.IP}) no soporta VCA.");

            return await provider.ObtenerModoActual(camara, ct);
        }

        public async Task<Resultado> CambiarModoAsync(Camara camara, VCAModoEnum nuevoModo, CancellationToken ct)
        {
            var provider = _vcaProviderResolver.Resolver(camara);

            if (provider is null)
                return Resultado.Fallo($"La cámara ({camara.IP}) no soporta VCA.");

            var modosSoportados = await provider.ObtenerModosSoportados(camara, ct);

            if (!modosSoportados.Exito) return Resultado.Fallo(modosSoportados.Error!);

            if (!modosSoportados.Valor!.Contains(nuevoModo))
                return Resultado.Fallo($"La cámara ({camara.IP}) no soporta el modo '{nuevoModo.Info().Titulo}'.");

            var resultado = await provider.CambiarModo(camara, nuevoModo, ct);

            if (!resultado.Exito) return resultado;

            if (!provider.RequiereReinicioAlCambiarModo) return Resultado.Ok();

            return await ReiniciarCamara(camara, ct);
        }

        public async Task<Resultado> ReiniciarCamara(Camara camara, CancellationToken ct)
        {
            try
            {
                using var client = _isapiClientFactoryService.Crear(camara);
                var response = await client.ExecuteAsync(new RestRequest("/ISAPI/System/reboot", Method.Put), ct);

                if (!response.IsSuccessful)
                    return Resultado.Fallo($"Error al reiniciar la cámara ({camara.IP}). HTTP {response.StatusCode}. {response.ErrorMessage}. {response.Content}");

                return Resultado.Ok();
            }
            catch
            {
                return Resultado.Fallo($"Excepción al reiniciar la cámara ({camara.IP})");
            }
        }
    }
}
