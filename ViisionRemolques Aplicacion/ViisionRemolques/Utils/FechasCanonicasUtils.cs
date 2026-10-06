namespace ViisionRemolques.Utils
{
    public static class FechasCanonicasUtils
    {
        public static (DateTime Inicio, DateTime Fin) Obtener(string tipo, DateTime fecha)
        {
            var inicio = fecha.Date;

            switch (tipo)
            {
                case "weekly":
                    int diasAlLunes = ((int)inicio.DayOfWeek + 6) % 7;
                    inicio = inicio.AddDays(-diasAlLunes);
                    return (inicio, inicio.AddDays(6).AddHours(23).AddMinutes(59).AddSeconds(59));

                case "monthly":
                    inicio = new DateTime(fecha.Year, fecha.Month, 1);
                    int ultimoDia = DateTime.DaysInMonth(fecha.Year, fecha.Month);
                    return (inicio, new DateTime(fecha.Year, fecha.Month, ultimoDia, 23, 59, 59));

                case "yearly":
                    inicio = new DateTime(fecha.Year, 1, 1);
                    return (inicio, new DateTime(fecha.Year, 12, 31, 23, 59, 59));

                default: // "daily"
                    return (inicio, inicio.AddHours(23).AddMinutes(59).AddSeconds(59));
            }
        }
    }
}
