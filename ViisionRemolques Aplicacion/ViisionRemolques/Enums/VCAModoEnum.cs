using System.ComponentModel;
using System.Reflection;

namespace ViisionRemolques.Enums
{
    public enum VCAModoEnum
    {
        AlarmaRecuentoPersonas,
        ANPR,
        ArmadoPistaPersona,
        CapturaFacial,
        DeteccionTipoMultiObjetivo,
        DeteccionTipoMultiObjetivoComparacion,
        EventoSmart,
        Monitorizacion,
        Ninguno,
        RecuentoPersonas,
        TraficoRodado
    }

    public record VcaModoInfo(string Titulo, string Descripcion);

    public static class VCAModoCatalogo
    {
        private static readonly Dictionary<VCAModoEnum, VcaModoInfo> Catalogo = new()
        {
            [VCAModoEnum.AlarmaRecuentoPersonas] = new VcaModoInfo("alarma_recuento_personas", "Alarma de recuento de personas"),
            [VCAModoEnum.ANPR] = new VcaModoInfo("anpr", "Reconocimiento de matrículas (ANPR)"),
            [VCAModoEnum.ArmadoPistaPersona] = new VcaModoInfo("armado_pista_persona", "Armado de pista de persona"),
            [VCAModoEnum.CapturaFacial] = new VcaModoInfo("captura_facial", "Captura facial"),
            [VCAModoEnum.DeteccionTipoMultiObjetivo] = new VcaModoInfo("deteccion_multiobjetivo", "Detección multi-objetivo"),
            [VCAModoEnum.DeteccionTipoMultiObjetivoComparacion] = new VcaModoInfo("deteccion_multiobjetivo_comparacion", "Detección multi-objetivo y Comparación"),
            [VCAModoEnum.EventoSmart] = new VcaModoInfo("evento_smart", "Evento Smart"),
            [VCAModoEnum.Monitorizacion] = new VcaModoInfo("monitorizacion", "Monitorización"),
            [VCAModoEnum.Ninguno] = new VcaModoInfo("ninguno", "Ninguno"),
            [VCAModoEnum.RecuentoPersonas] = new VcaModoInfo("recuento_personas", "Recuento de personas"),
            [VCAModoEnum.TraficoRodado] = new VcaModoInfo("trafico_rodado", "Tráfico rodado"),
        };

        public static VcaModoInfo Info(this VCAModoEnum modo) => 
            Catalogo.TryGetValue(modo, out var info) ? info : new VcaModoInfo(modo.ToString(), modo.ToString());

        public static readonly Dictionary<string, VCAModoEnum> ModoPorTitulo = 
            Catalogo.ToDictionary(kv => kv.Value.Titulo, kv => kv.Key, StringComparer.OrdinalIgnoreCase);

        public static bool TryParseTitulo(string titulo, out VCAModoEnum modo) =>
            ModoPorTitulo.TryGetValue(titulo, out modo);
    }
}
