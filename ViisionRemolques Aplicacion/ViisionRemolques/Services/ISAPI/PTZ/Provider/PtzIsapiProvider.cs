using RestSharp;
using System.Xml.Linq;
using ViisionRemolques.Repositories;
using ViisionRemolques.Utils;

namespace ViisionRemolques.Services.ISAPI.PTZ.Provider
{
    public class PtzIsapiProvider
    {
        private readonly IsapiClientFactoryService _isapiClientFactoryService;

        public PtzIsapiProvider(IsapiClientFactoryService isapiClientFactoryService)
        {
            _isapiClientFactoryService = isapiClientFactoryService;
        }

        public async Task<Resultado> MoverAsync(CamaraEntity camara, int pan, int tilt, CancellationToken ct)
        {
            try
            {
                using var client = _isapiClientFactoryService.Crear(camara);

                var xml = new XDocument(
                    new XDeclaration("1.0", "UTF-8", null),
                    new XElement("PTZData",
                        new XElement("pan", pan),
                        new XElement("tilt", tilt)));

                var request = new RestRequest("/ISAPI/PTZCtrl/channels/1/continuous", Method.Put);
                request.AddStringBody(xml.ToString(), DataFormat.Xml);

                var response = await client.ExecuteAsync(request, ct);

                if (!response.IsSuccessStatusCode)
                    return Resultado.Fallo($"Error al mover PTZ ({camara.IP}). HTTP {response.StatusCode}. {response.Content}");

                return Resultado.Ok();
            }
            catch (Exception ex)
            {
                return Resultado.Fallo($"Excepción al mover PTZ ({camara.IP}): {ex.Message}");
            }
        }

        public async Task<Resultado> ZoomAsync(CamaraEntity camara, int valor, CancellationToken ct)
        {
            try
            {
                using var client = _isapiClientFactoryService.Crear(camara);

                var xml = new XDocument(
                    new XDeclaration("1.0", "UTF-8", null),
                    new XElement("PTZData",
                        new XElement("zoom", valor)));

                var request = new RestRequest("/ISAPI/PTZCtrl/channels/1/continuous", Method.Put);
                request.AddStringBody(xml.ToString(), DataFormat.Xml);

                var response = await client.ExecuteAsync(request, ct);

                if (!response.IsSuccessStatusCode)
                    return Resultado.Fallo($"Error al hacer zoom ({camara.IP}). HTTP {response.StatusCode}. {response.Content}");

                return Resultado.Ok();
            }
            catch (Exception ex)
            {
                return Resultado.Fallo($"Excepción al hacer zoom ({camara.IP}): {ex.Message}");
            }
        }

        public async Task<Resultado> EnfocarAsync(CamaraEntity camara, int valor, CancellationToken ct)
        {
            try
            {
                using var client = _isapiClientFactoryService.Crear(camara);

                var xml = new XDocument(
                    new XDeclaration("1.0", "UTF-8", null),
                    new XElement("FocusData", new XElement("focus", valor)));

                var request = new RestRequest("/ISAPI/System/Video/inputs/channels/1/focus", Method.Put);
                request.AddStringBody(xml.ToString(), DataFormat.Xml);

                var response = await client.ExecuteAsync(request, ct);

                if (!response.IsSuccessStatusCode)
                    return Resultado.Fallo($"Error al enfocar ({camara.IP}). HTTP {response.StatusCode}. {response.Content}");

                return Resultado.Ok();
            }
            catch (Exception ex)
            {
                return Resultado.Fallo($"Excepción al enfocar ({camara.IP}): {ex.Message}");
            }
        }

        public async Task<Resultado> AjustarIrisAsync(CamaraEntity camara, int valor, CancellationToken ct)
        {
            try
            {
                using var client = _isapiClientFactoryService.Crear(camara);

                var xml = new XDocument(
                    new XDeclaration("1.0", "UTF-8", null),
                    new XElement("IrisData", new XElement("iris", valor)));

                var request = new RestRequest("/ISAPI/System/Video/inputs/channels/1/iris", Method.Put);
                request.AddStringBody(xml.ToString(), DataFormat.Xml);

                var response = await client.ExecuteAsync(request, ct);

                if (!response.IsSuccessStatusCode)
                    return Resultado.Fallo($"Error al ajustar iris ({camara.IP}). HTTP {response.StatusCode}. {response.Content}");

                return Resultado.Ok();
            }
            catch (Exception ex)
            {
                return Resultado.Fallo($"Excepción al ajustar iris ({camara.IP}): {ex.Message}");
            }
        }

        public async Task<Resultado> EscobillaAsync(CamaraEntity camara, CancellationToken ct)
        {
            try
            {
                using var client = _isapiClientFactoryService.Crear(camara);

                var request = new RestRequest("/ISAPI/PTZCtrl/channels/1/manualWiper", Method.Put);
                var response = await client.ExecuteAsync(request, ct);

                if (!response.IsSuccessStatusCode)
                    return Resultado.Fallo($"Error al activar la escobilla ({camara.IP}). HTTP {response.StatusCode}. {response.Content}");

                return Resultado.Ok();
            }
            catch (Exception ex)
            {
                return Resultado.Fallo($"Excepción al activar la escobilla ({camara.IP}): {ex.Message}");
            }
        }

        public async Task<Resultado> EnfoqueAuxiliarAsync(CamaraEntity camara, CancellationToken ct)
        {
            try
            {
                using var client = _isapiClientFactoryService.Crear(camara);

                var request = new RestRequest("/ISAPI/PTZCtrl/channels/1/onepushfoucs/start", Method.Put);
                var response = await client.ExecuteAsync(request, ct);

                if (!response.IsSuccessStatusCode)
                    return Resultado.Fallo($"Error al ejecutar enfoque auxiliar ({camara.IP}). HTTP {response.StatusCode}. {response.Content}");

                return Resultado.Ok();
            }
            catch (Exception ex)
            {
                return Resultado.Fallo($"Excepción al ejecutar enfoque auxiliar ({camara.IP}): {ex.Message}");
            }
        }

        public async Task<Resultado> InicializarObjetivoAsync(CamaraEntity camara, CancellationToken ct)
        {
            try
            {
                using var client = _isapiClientFactoryService.Crear(camara);

                var request = new RestRequest("/ISAPI/PTZCtrl/channels/1/onepushfoucs/reset", Method.Put);
                var response = await client.ExecuteAsync(request, ct);

                if (!response.IsSuccessStatusCode)
                    return Resultado.Fallo($"Error al inicializar el objetivo ({camara.IP}). HTTP {response.StatusCode}. {response.Content}");

                return Resultado.Ok();
            }
            catch (Exception ex)
            {
                return Resultado.Fallo($"Excepción al inicializar el objetivo ({camara.IP}): {ex.Message}");
            }
        }

        public async Task<Resultado> CalibrarZoomAsync(CamaraEntity camara, CancellationToken ct)
        {
            try
            {
                using var client = _isapiClientFactoryService.Crear(camara);

                var request = new RestRequest("/ISAPI/PTZCtrl/channels/1/FoucsCalibrat", Method.Put);
                request.AddStringBody("{\"calibrateType\":\"zoom\"}", DataFormat.Json);

                var response = await client.ExecuteAsync(request, ct);

                if (!response.IsSuccessStatusCode)
                    return Resultado.Fallo($"Error al calibrar zoom ({camara.IP}). HTTP {response.StatusCode}. {response.Content}");

                return Resultado.Ok();
            }
            catch (Exception ex)
            {
                return Resultado.Fallo($"Excepción al calibrar zoom ({camara.IP}): {ex.Message}");
            }
        }
    }
}
