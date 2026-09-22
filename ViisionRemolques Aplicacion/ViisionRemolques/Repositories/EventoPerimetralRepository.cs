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

        public async Task<long> InsertarAsync(EventoPerimetral evento)
        {
            var parameters = new DynamicParameters();
            parameters.Add("@PId", evento.PId, DbType.String, ParameterDirection.Input, 64);
            parameters.Add("@IPCamara", evento.IPCamara, DbType.String, ParameterDirection.Input, 45);
            parameters.Add("@Evento", evento.Evento, DbType.String, ParameterDirection.Input, 50);
            parameters.Add("@ReglaId", evento.ReglaId, DbType.String, ParameterDirection.Input, 64);
            parameters.Add("@TipoObjetivo", evento.TipoObjetivo, DbType.String, ParameterDirection.Input, 30);
            parameters.Add("@FechaEvento", evento.FechaEvento, DbType.DateTime2, ParameterDirection.Input);
            parameters.Add("@PathImagen", evento.PathImagen, DbType.String, ParameterDirection.Input, 500);
            parameters.Add("@IdExterno", evento.IdExterno, DbType.Int64, ParameterDirection.Input);
            parameters.Add("@Sincronizado", evento.Sincronizado, DbType.Boolean, ParameterDirection.Input);
            parameters.Add("@IdInternoGenerado", dbType: DbType.Int64, direction: ParameterDirection.Output);

            await _dbConnection.ExecuteAsync(
                "dbo.sp_EventosPerimetrales_Insertar",
                parameters,
                commandType: CommandType.StoredProcedure
            );

            long idGenerado = parameters.Get<long>("@IdInternoGenerado");
            evento.IdInterno = idGenerado;

            return idGenerado;
        }
    }
}