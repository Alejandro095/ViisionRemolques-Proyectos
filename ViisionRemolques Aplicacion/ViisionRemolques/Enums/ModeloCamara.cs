using System.ComponentModel;
using System.Reflection;

namespace ViisionRemolques.Enums
{
    public enum ModeloCamaraEnum
    {
        [Description("DS2DF8C842IXG1ELWY")]
        HikvisionPTZ,

        [Description("DS2CD3687G3TLIZSU")]
        HikvisionBala,

        [Description("DS2CD6365G1IVS")]
        Hikvision360,
        [Description("iDSTCM403GIR")]
        HikvisionRadar,
        [Description("DS2CD3T87G3PLISUYSL")]
        Hikvision180
    }

    public static class ModeloCamaraEnumExtensions
    {
        public static string ObtenerCodigoModelo(this ModeloCamaraEnum modelo)
        {
            var field = modelo.GetType().GetField(modelo.ToString());
            var attribute = field?.GetCustomAttribute<DescriptionAttribute>();
            return attribute?.Description ?? modelo.ToString();
        }
    }
}
