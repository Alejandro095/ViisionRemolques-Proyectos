using System.ComponentModel;
using System.Reflection;

namespace ViisionRemolques.Enums
{
    public enum VCAModoEnum
    {
        [Description("")]
        Ninguno,
        [Description("A")]
        DeteccionTipoMultiObjeto,
        [Description("B")]
        ComparacionTipoMultiObjeto,
        [Description("C")]
        ArmadoPistaPersona,
        [Description("D")]
        EventoSmart,
        [Description("smart")]
        TraficoRodado,
        [Description("F")]
        Monitorizacion,
        [Description("G")]
        CapturaFacial,
        [Description("H")]
        AlarmaRecuentoPersonas,
        [Description("I")]
        RecuentoPersonas
    }   

    public static class VCAModoEnumExtensions
    {
        public static string Val(this VCAModoEnum modelo)
        {
            var field = modelo.GetType().GetField(modelo.ToString());
            var attribute = field?.GetCustomAttribute<DescriptionAttribute>();
            return attribute?.Description ?? modelo.ToString();
        }
    }
}
