using ViisionRemolques.Entities;
using ViisionRemolques.Enums;
using ViisionRemolques.Utils;

namespace ViisionRemolques.Services.ISAPI.VCA
{
    public interface IVcaProvider
    {
        bool RequiereReinicioAlCambiarModo { get; }

        Task<Resultado<IEnumerable<VCAModoEnum>>> ObtenerModosSoportados(Camara camara, CancellationToken ct);
        Task<Resultado<VCAModoEnum>> ObtenerModoActual(Camara camara, CancellationToken ct);
        Task<Resultado> CambiarModo(Camara camara, VCAModoEnum vcaModo, CancellationToken ct);
    }
}
