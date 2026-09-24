using System.Data;
using Dapper;
using ViisionRemolques.Entities;

namespace ViisionRemolques.Repositories
{
    public class ImagenesRepository
    {
        private readonly IDbConnection _dbConnection;

        public ImagenesRepository(IDbConnection dbConnection)
        {
            _dbConnection = dbConnection;
        }

        public async Task InsertarAsync(Imagen imagen)
        {
            var parameters = new DynamicParameters();
            parameters.Add("@OrigenTabla", imagen.OrigenTabla, DbType.String, ParameterDirection.Input, 50);
            parameters.Add("@OrigenIdInterno", imagen.OrigenIdInterno, DbType.Int64);
            parameters.Add("@PathImagen", imagen.PathImagen, DbType.String, ParameterDirection.Input, 500);
            parameters.Add("@Sincronizado", imagen.Sincronizado, DbType.Boolean);
            parameters.Add("@IdInternoGenerado", dbType: DbType.Int64, direction: ParameterDirection.Output);

            await _dbConnection.ExecuteAsync(
                "dbo.sp_Imagenes_Insertar",
                parameters,
                commandType: CommandType.StoredProcedure
            );
        }
    }
}