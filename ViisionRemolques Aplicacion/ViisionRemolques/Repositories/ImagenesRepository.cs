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
            await _dbConnection.ExecuteAsync(
                "dbo.sp_Imagenes_Insertar",
                new
                {
                    OrigenTabla = imagen.OrigenTabla,
                    OrigenIdInterno = imagen.OrigenIdInterno,
                    PathImagen = imagen.PathImagen,
                    Sincronizado = imagen.Sincronizado
                },
                commandType: CommandType.StoredProcedure
            );
        }
    }
}