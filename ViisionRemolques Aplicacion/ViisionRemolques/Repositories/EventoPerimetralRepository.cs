using System.Data;
using Dapper;
using ViisionRemolques.Entities;

namespace ViisionRemolques.Repositories
{
    public class EventoPerimetralRepository
    {
        private readonly IDbConnection _dbConnection;

        public EventoPerimetralRepository(IDbConnection dbConnection)
        {
            _dbConnection = dbConnection;
        }

        public async Task InsertarAsync(EventoPerimetral evento)
        {
            var parameters = new DynamicParameters();
            parameters.Add("@IPCamara", evento.IPCamara, DbType.String);
            parameters.Add("@Evento", evento.Evento, DbType.String);
            parameters.Add("@ReglaId", evento.ReglaId, DbType.String);
            parameters.Add("@ZonaDeteccion", evento.ZonaDeteccion, DbType.String);
            parameters.Add("@TipoObjetivo", evento.TipoObjetivo, DbType.String);
            parameters.Add("@FechaEvento", evento.FechaEvento, DbType.DateTime2);
            parameters.Add("@PathImagen", evento.PathImagen, DbType.String);
            parameters.Add("@IdExterno", evento.IdExterno, DbType.Int64);
            parameters.Add("@Sincronizado", evento.Sincronizado, DbType.Boolean);

            await _dbConnection.ExecuteAsync(
                "dbo.sp_EventosPerimetrales_Insertar",
                parameters,
                commandType: CommandType.StoredProcedure
            );
        }
    }
}