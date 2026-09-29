using Dapper;
using System.Data;
using System.Data.Common;

namespace ViisionRemolques.Repositories
{
    public class ImagenesRepository
    {
        private readonly DbConnection _dbConnection;

        public ImagenesRepository(DbConnection dbConnection)
        {
            _dbConnection = dbConnection;
        }

        public async Task InsertarAsync(
            long eventoId,
            List<string> paths,
            IDbTransaction? transaction = null)
        {
            if (paths == null || !paths.Any())
                return;

            const string sql = @"
                INSERT INTO dbo.Imagenes (
                    EventoIdInterno,
                    Path
                )
                VALUES (
                    @EventoIdInterno,
                    @Path
                );";

            // Se asocia cada ruta con la FK del evento
            var parametros = paths.Select(path => new
            {
                EventoIdInterno = eventoId,
                Path = path
            });

            var conn = transaction?.Connection ?? _dbConnection;

            await conn.ExecuteAsync(sql, parametros, transaction: transaction);
        }
    }

    public class ImagenEntity
    {
        public long IdInterno { get; set; }
        public string Path { get; set; } = string.Empty;
        public bool Sincronizado { get; set; }
        public DateTime FechaCreacion { get; set; }
    }
}
