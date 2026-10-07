namespace ViisionRemolques.Utils
{
    public readonly struct Resultado<T>
    {
        public bool Exito { get; }
        public T? Valor { get; }
        public string? Error { get; }

        private Resultado(bool exito, T? valor, string? error) =>
            (Exito, Valor, Error) = (exito, valor, error);

        public static Resultado<T> Ok(T valor) => new(true, valor, null);
        public static Resultado<T> Fallo(string error) => new(false, default, error);
    }

    public readonly struct Resultado
    {
        public bool Exito { get; }
        public string? Error { get; }

        private Resultado(bool exito, string? error) => (Exito, Error) = (exito, error);

        public static Resultado Ok() => new(true, null);
        public static Resultado Fallo(string error) => new(false, error);
    }
}
