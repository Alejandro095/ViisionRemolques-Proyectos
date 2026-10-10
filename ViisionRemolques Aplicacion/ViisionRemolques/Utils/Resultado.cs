namespace ViisionRemolques.Utils
{
    public enum TipoError
    {
        Usuario,
        Excepcion
    }

    public readonly struct Resultado<T>
    {
        public bool Exito { get; }
        public T? Valor { get; }
        public string? Error { get; }
        public Exception? Excepcion { get; }
        public TipoError? Tipo { get; }

        private Resultado(bool exito, T? valor, string? mensaje, Exception? excepcion)
        {
            Exito = exito;
            Valor = valor;
            Error = mensaje ?? excepcion?.Message;
            Excepcion = excepcion;

            if (!exito)
            {
                Tipo = excepcion != null ? TipoError.Excepcion : TipoError.Usuario;
            }
            else
            {
                Tipo = null;
            }
        }

        public static Resultado<T> Ok(T valor) =>
            new(true, valor, null, null);
        public static Resultado<T> Fallo(string mensaje) =>
            new(false, default, mensaje, null);
        public static Resultado<T> Fallo(Exception excepcion) =>
            new(false, default, null, excepcion);
        public static Resultado<T> Fallo(string mensaje, Exception excepcion) =>
            new(false, default, mensaje, excepcion);

        public Exception? ObtenerExcepcion()
        {
            if (Exito) return null;

            if (Excepcion != null) return Excepcion;

            return new Exception(Error ?? "Ocurrió un error desconocido.");
        }
        public void LanzarErrorSiFalla()
        {
            if (!Exito) throw ObtenerExcepcion()!;
        }
        public string? ObtenerMensajeSeguro(string mensajePorDefecto = "Ocurrió un error inesperado. Intente más tarde.")
        {
            if (Exito) return null;

            if (Tipo == TipoError.Usuario)
            {
                return Error;
            }

            return mensajePorDefecto;
        }
    }

    public readonly struct Resultado
    {
        public bool Exito { get; }
        public string? Error { get; }
        public Exception? Excepcion { get; }
        public TipoError? Tipo { get; }

        private Resultado(bool exito, string? mensaje, Exception? excepcion)
        {
            Exito = exito;
            Error = mensaje ?? excepcion?.Message;
            Excepcion = excepcion;

            if (!exito)
            {
                Tipo = excepcion != null ? TipoError.Excepcion : TipoError.Usuario;
            }
            else
            {
                Tipo = null;
            }
        }

        public static Resultado Ok() =>
            new(true, null, null);
        public static Resultado Fallo(string mensaje) =>
            new(false, mensaje, null);
        public static Resultado Fallo(Exception excepcion) =>
            new(false, null, excepcion);
        public static Resultado Fallo(string mensaje, Exception excepcion) =>
            new(false, mensaje, excepcion);

        public Exception? ObtenerExcepcion()
        {
            if (Exito) return null;

            if (Excepcion != null) return Excepcion;

            return new Exception(Error ?? "Ocurrió un error desconocido.");
        }
        public void LanzarErrorSiFalla()
        {
            if (!Exito) throw ObtenerExcepcion()!;
        }
        public string? ObtenerMensajeSeguro(string mensajePorDefecto = "Ocurrió un error inesperado. Intente más tarde.")
        {
            if (Exito) return null;

            if (Tipo == TipoError.Usuario)
            {
                return Error;
            }

            return mensajePorDefecto;
        }
    }
}