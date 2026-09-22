using Dapper;
using System.Data;
using ViisionRemolques.Entities;

namespace ViisionRemolques.Repositories
{
    public class AlarmaDesconocidaLogRepository
    {
        private readonly IDbConnection _dbConnection;

        public AlarmaDesconocidaLogRepository(IDbConnection dbConnection)
        {
            _dbConnection = dbConnection;
        }

        public async Task InsertarAsync(AlarmaDesonocidaLogEntity log)
        {
            await _dbConnection.ExecuteAsync(
                "dbo.sp_AlarmasDesconocidasLog_Insertar",
                new
                {
                    IPCamara = log.IPCamara,
                    ContentType = log.ContentType,
                    Evento = log.Evento,
                    Body = log.Body,
                    Motivo = log.Motivo
                },
                commandType: CommandType.StoredProcedure
            );
        }
    }
}