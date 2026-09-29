using Dapper;
using System.Data;
using System.Data.Common;
using ViisionRemolques.Entities;

namespace ViisionRemolques.Repositories
{
    public class CamaraRepository
    {
        private readonly DbConnection _dbConnection;

        public CamaraRepository(DbConnection dbConnection)
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
