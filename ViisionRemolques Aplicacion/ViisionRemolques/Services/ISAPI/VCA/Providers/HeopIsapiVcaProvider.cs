using RestSharp;
using System.Xml.Linq;
using ViisionRemolques.Enums;
using ViisionRemolques.Repositories;
using ViisionRemolques.Utils;

namespace ViisionRemolques.Services.ISAPI.VCA.Providers
{
    public class HeopIsapiVcaProvider : IVcaProvider
    {
        private readonly IsapiClientFactoryService _isapiClientFactoryService;

        bool IVcaProvider.RequiereReinicioAlCambiarModo => false;

        private static readonly Dictionary<VCAModoEnum, int> AppIdPorModo = new()
        {
            [VCAModoEnum.AlarmaRecuentoPersonas] = 8999,
            [VCAModoEnum.CapturaFacial] = 12000,
            [VCAModoEnum.EventoSmart] = 15000,
            [VCAModoEnum.RecuentoPersonas] = 12345,
        };

        private static readonly Dictionary<int, VCAModoEnum> ModoPorAppId =
            AppIdPorModo.ToDictionary(kv => kv.Value, kv => kv.Key);

        public HeopIsapiVcaProvider(IsapiClientFactoryService isapiClientFactoryService)
        {
            _isapiClientFactoryService = isapiClientFactoryService;
        }

        public async Task<Resultado<IEnumerable<VCAModoEnum>>> ObtenerModosSoportados(CamaraEntity camara, CancellationToken ct)
        {
            var apps = await ObtenerAppsAsync(camara, ct);
            if (!apps.Exito) return Resultado<IEnumerable<VCAModoEnum>>.Fallo(apps.Error!);

            var modos = apps.Valor!
                .Where(a => ModoPorAppId.ContainsKey(a.AppId))
                .Select(a => ModoPorAppId[a.AppId]);

            return Resultado<IEnumerable<VCAModoEnum>>.Ok(modos);
        }

        public async Task<Resultado> CambiarModo(CamaraEntity camara, VCAModoEnum vcaModo, CancellationToken ct)
        {
            if (!AppIdPorModo.TryGetValue(vcaModo, out var appIdDestino))
                return Resultado.Fallo("El VCA no ha sido registrado en el catalogo de modos.");

            var apps = await ObtenerAppsAsync(camara, ct);
            if (!apps.Exito) return Resultado.Fallo(apps.Error!);

            var target = apps.Valor!.FirstOrDefault(a => a.AppId == appIdDestino);
            if (target is null)
                return Resultado.Fallo($"La app para el modo '{vcaModo.Info().Titulo}' no está instalada en la cámara ({camara.IP}).");

            foreach (var activa in apps.Valor!.Where(a =>
                a.RunStatus &&
                a.Id != target.Id &&
                ModoPorAppId.ContainsKey(a.AppId)))
            {
                var apagar = await SetRunStatusAsync(camara, activa.Id, false, ct);
                if (!apagar.Exito) return apagar;
            }

            if (target.RunStatus) return Resultado.Ok();

            return await SetRunStatusAsync(camara, target.Id, true, ct);
        }

        public async Task<Resultado<VCAModoEnum>> ObtenerModoActual(CamaraEntity camara, CancellationToken ct)
        {
            var apps = await ObtenerAppsAsync(camara, ct);

            if (!apps.Exito) 
                return Resultado<VCAModoEnum>.Fallo(apps.Error!);

            var activa = apps.Valor!.FirstOrDefault(a => a.RunStatus);

            if (activa is null) 
                return Resultado<VCAModoEnum>.Ok(VCAModoEnum.Ninguno);

            if (!ModoPorAppId.TryGetValue(activa.AppId, out var modo))
                return Resultado<VCAModoEnum>.Fallo("La cámara tiene un modo VCA activo que no está soportado por esta aplicación.");

            return Resultado<VCAModoEnum>.Ok(modo);
        }

        private async Task<Resultado> SetRunStatusAsync(CamaraEntity camara, int appListId, bool runStatus, CancellationToken ct)
        {
            try
            {
                using var client = _isapiClientFactoryService.Crear(camara);

                var accion = runStatus ? "start" : "stop";
                var request = new RestRequest($"/ISAPI/Custom/OpenPlatform/App/{appListId}/{accion}", Method.Put);

                var response = await client.ExecuteAsync(request, ct);

                if (!response.IsSuccessStatusCode)
                    return Resultado.Fallo($"Error al {accion} la app id={appListId} ({camara.IP}). HTTP {response.StatusCode}");

                return Resultado.Ok();
            }
            catch
            {
                return Resultado.Fallo($"Excepción al cambiar estado de la app ({camara.IP})");
            }
        }

        private record AppInfo(int Id, int AppId, bool RunStatus);

        private async Task<Resultado<List<AppInfo>>> ObtenerAppsAsync(CamaraEntity camara, CancellationToken ct)
        {
            try
            {
                using var client = _isapiClientFactoryService.Crear(camara);

                var response = await client.ExecuteAsync(new RestRequest(
                    "/ISAPI/Custom/OpenPlatform/App",
                    Method.Get
                ), ct);

                if (!response.IsSuccessful || string.IsNullOrWhiteSpace(response.Content))
                    return Resultado<List<AppInfo>>.Fallo(
                        $"Error al consultar las apps VCA de la cámara ({camara.IP}). HTTP {response.StatusCode}.");

                var doc = XDocument.Parse(response.Content);

                var apps = doc.Descendants()
                    .Where(e => e.Name.LocalName == "App")
                    .Select(app => new AppInfo(
                        Id: int.Parse(app.Elements().First(e => e.Name.LocalName == "id").Value),
                        AppId: int.Parse(app.Elements().First(e => e.Name.LocalName == "AppID").Value),
                        RunStatus: bool.Parse(app.Elements().First(e => e.Name.LocalName == "runStatus").Value)))
                    .ToList();

                return Resultado<List<AppInfo>>.Ok(apps);
            }
            catch
            {
                return Resultado<List<AppInfo>>.Fallo($"Excepción al consultar las apps VCA ({camara.IP})");
            }
        }
    }
}
