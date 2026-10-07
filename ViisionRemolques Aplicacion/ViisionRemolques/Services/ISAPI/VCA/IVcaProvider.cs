using ViisionRemolques.Enums;
using ViisionRemolques.Repositories;
using ViisionRemolques.Utils;

namespace ViisionRemolques.Services.ISAPI.VCA
{
    public interface IVcaProvider
    {
        bool RequiereReinicioAlCambiarModo { get; }

        Task<Resultado<IEnumerable<VCAModoEnum>>> ObtenerModosSoportados(CamaraEntity camara, CancellationToken ct);
        Task<Resultado<VCAModoEnum>> ObtenerModoActual(CamaraEntity camara, CancellationToken ct);
        Task<Resultado> CambiarModo(CamaraEntity camara, VCAModoEnum vcaModo, CancellationToken ct);
    }
}
