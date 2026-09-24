using Dapper;
using System.Data;
using ViisionRemolques.Entities;

namespace ViisionRemolques.Repositories
{
    public class EventoAlertaConteoPersonaRepository
    {
        private readonly IDbConnection _dbConnection;

        public EventoAlertaConteoPersonaRepository(IDbConnection dbConnection)
        {
            _dbConnection = dbConnection;
        }

        public async Task InsertarAsync(EventoAlertaConteoPersonaEntity evento)
        {
            await _dbConnection.ExecuteAsync(
                "dbo.sp_EventosConteosPersonas_Insertar",
                new
                {
                    IPCamara = evento.IPCamara,
                    Evento = evento.Evento,
                    Regiones = evento.Regiones,
                    TotalEntradas = evento.TotalEntradas,
                    TotalSalidas = evento.TotalSalidas,
                    TotalPasos = evento.TotalPasos,
                    TotalDuplicados = evento.TotalDuplicados,
                    FechaEvento = evento.FechaEvento,
                    Prioridad = evento.Prioridad,
                    IdExterno = evento.IdExterno,
                    Sincronizado = evento.Sincronizado
                },
                commandType: CommandType.StoredProcedure
            );
        }
    }
}
