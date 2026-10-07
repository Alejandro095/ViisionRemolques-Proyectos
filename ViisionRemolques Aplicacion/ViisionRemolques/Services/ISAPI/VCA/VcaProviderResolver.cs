using ViisionRemolques.Enums;
using ViisionRemolques.Repositories;
using ViisionRemolques.Services.ISAPI.VCA.Providers;

namespace ViisionRemolques.Services.ISAPI.VCA
{
    public class VcaProviderResolver
    {
        private readonly LegacyIsapiVcaProvider _legacyIsapiVcaProvider;
        private readonly HeopIsapiVcaProvider _heopIsapiVcaProvider;

        public VcaProviderResolver(LegacyIsapiVcaProvider legacyIsapiVcaProvider, HeopIsapiVcaProvider heopIsapiVcaProvider)
        {
            _legacyIsapiVcaProvider = legacyIsapiVcaProvider;
            _heopIsapiVcaProvider = heopIsapiVcaProvider;
        }

        public IVcaProvider? Resolver(CamaraEntity camara) => camara.PlataformaVCA switch
        {
            PlataformaVcaEnum.NA => null,
            PlataformaVcaEnum.Legacy => _legacyIsapiVcaProvider,
            PlataformaVcaEnum.Heop => _heopIsapiVcaProvider,
            _ => throw new NotSupportedException($"Plataforma VCA '{camara.PlataformaVCA}' sin provider."),
        };
    }
}
