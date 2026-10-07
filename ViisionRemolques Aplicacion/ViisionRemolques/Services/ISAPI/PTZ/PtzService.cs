using ViisionRemolques.Repositories;
using ViisionRemolques.Services.ISAPI.PTZ.Provider;
using ViisionRemolques.Utils;

namespace ViisionRemolques.Services.ISAPI.PTZ
{
    public class PtzService
    {
        private readonly PtzIsapiProvider _driver;

        public PtzService(PtzIsapiProvider driver)
        {
            _driver = driver;
        }

        public async Task<Resultado> MoverAsync(CamaraEntity camara, int pan, int tilt, CancellationToken ct)
        {
            if (!camara.Soporta_PtzMovimiento)
                return Resultado.Fallo($"La cámara ({camara.IP}) no soporta movimiento PTZ.");

            return await _driver.MoverAsync(camara, pan, tilt, ct);
        }

        public async Task<Resultado> ZoomAsync(CamaraEntity camara, int valor, CancellationToken ct)
        {
            if (!camara.Soporta_PtzZoom)
                return Resultado.Fallo($"La cámara ({camara.IP}) no soporta zoom.");

            return await _driver.ZoomAsync(camara, valor, ct);
        }

        public async Task<Resultado> EnfocarAsync(CamaraEntity camara, int valor, CancellationToken ct)
        {
            if (!camara.Soporta_PtzEnfoque)
                return Resultado.Fallo($"La cámara ({camara.IP}) no soporta enfoque.");

            return await _driver.EnfocarAsync(camara, valor, ct);
        }

        public async Task<Resultado> AjustarIrisAsync(CamaraEntity camara, int valor, CancellationToken ct)
        {
            if (!camara.Soporta_PtzIris)
                return Resultado.Fallo($"La cámara ({camara.IP}) no soporta iris.");

            return await _driver.AjustarIrisAsync(camara, valor, ct);
        }

        public async Task<Resultado> EscobillaAsync(CamaraEntity camara, CancellationToken ct)
        {
            if (!camara.Soporta_PtzEscobilla)
                return Resultado.Fallo($"La cámara ({camara.IP}) no soporta escobilla.");

            return await _driver.EscobillaAsync(camara, ct);
        }

        public async Task<Resultado> EnfoqueAuxiliarAsync(CamaraEntity camara, CancellationToken ct)
        {
            if (!camara.Soporta_PtzEnfoqueAuxiliar)
                return Resultado.Fallo($"La cámara ({camara.IP}) no soporta enfoque auxiliar.");

            return await _driver.EnfoqueAuxiliarAsync(camara, ct);
        }

        public async Task<Resultado> InicializarObjetivoAsync(CamaraEntity camara, CancellationToken ct)
        {
            if (!camara.Soporta_PtzInicializacionObjetivo)
                return Resultado.Fallo($"La cámara ({camara.IP}) no soporta inicialización del objetivo.");

            return await _driver.InicializarObjetivoAsync(camara, ct);
        }

        public async Task<Resultado> CalibrarZoomAsync(CamaraEntity camara, CancellationToken ct)
        {
            if (!camara.Soporta_PtzCalibracionZoom)
                return Resultado.Fallo($"La cámara ({camara.IP}) no soporta calibración de zoom.");

            return await _driver.CalibrarZoomAsync(camara, ct);
        }
    }
}
