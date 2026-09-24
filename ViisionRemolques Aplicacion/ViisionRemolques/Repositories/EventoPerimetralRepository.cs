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
            await _dbConnection.ExecuteAsync(
                "dbo.sp_EventosPerimetrales_Insertar",
                new
                {
                    IPCamara = evento.IPCamara,
                    Evento = evento.Evento,
                    RegionId = evento.RegionId,
                    ZonaDeteccion = evento.ZonaDeteccion,
                    TipoObjetivo = evento.TipoObjetivo,
                    FechaEvento = evento.FechaEvento,
                    PathImagen = evento.PathImagen,
                    Sincronizado = evento.Sincronizado,
                    Prioridad = evento.Prioridad
                },
                commandType: CommandType.StoredProcedure
            );
        }
    }
}