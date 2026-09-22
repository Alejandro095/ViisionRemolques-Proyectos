using System.Data;
using ViisionRemolques.Entities;
using Dapper;

namespace ViisionRemolques.Repositories
{
    public class CamaraRepository
    {
        private readonly IDbConnection _dbConnection;

        public CamaraRepository(IDbConnection dbConnection)
        {
            _dbConnection = dbConnection;
        }

        public async Task<IEnumerable<Camara>> ObtenerTodasAsync()
        {
            return await _dbConnection.QueryAsync<Camara>(
                "sp_Camaras_ObtenerTodas",
                commandType: CommandType.StoredProcedure
            );
        }

        public async Task<Camara?> ObtenerPorIdInternoAsync(long idInterno)
        {
            return await _dbConnection.QueryFirstOrDefaultAsync<Camara>(
                "sp_Camaras_BuscarPorIdInterno",
                new { IdInterno = idInterno },
                commandType: CommandType.StoredProcedure
            );
        }
    }
}
