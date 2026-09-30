using System.ComponentModel;
using System.Reflection;

namespace ViisionRemolques.Enums
{
    public enum VCAModoEnum
    {
        [Description("TEMP-AlarmaRecuentoPersonas")]
        AlarmaRecuentoPersonas,

        [Description("ANPR")]
        ANPR,

        [Description("personArming")]
        ArmadoPistaPersona,

        [Description("TEMP-CapturaFacial")]
        CapturaFacial,

        [Description("mixedTargetDetection")]
        DeteccionTipoMultiObjetivo,

        [Description("faceHumanModelingContrast")]
        DeteccionTipoMultiObjetivoComparacion,

        [Description("smart")]
        EventoSmart,

        [Description("close")]
        Monitorizacion,

        [Description("")]
        Ninguno,

        [Description("TEMP-RecuentoPersonas")]
        RecuentoPersonas,

        [Description("roadDetection")]
        TraficoRodado        
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
