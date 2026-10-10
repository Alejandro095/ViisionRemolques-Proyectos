using Dapper;
using System.Data;
using System.Data.Common;
using ViisionRemolques.Enums;
using ViisionRemolques.Parsing.Extractors;

namespace ViisionRemolques.Repositories.Eventos
{
    public abstract class EventoBaseRepository
    {
        protected readonly DbConnection _dbConnection;

        protected EventoBaseRepository(DbConnection dbConnection)
        {
            _dbConnection = dbConnection;
        }

        protected async Task<long> InsertarEventoBaseAsync(
            EventoBaseExtractorModelo eventoBaseExtractorModelo, 
            int Prioridad = 5, 
            string? Payload = null, 
            IDbTransaction? transaction = null)
        {
            const string sql = @"
                INSERT INTO dbo.Eventos (
                    CamaraIP,
                    CamaraMAC,
                    VCA,
                    Evento,
                    Prioridad,
                    Payload,
                    FechaEvento
                )
                VALUES (
                    @CamaraIP,
                    @CamaraMAC,
                    @VCA,
                    @Evento,
                    @Prioridad,
                    @Payload,
                    @FechaEvento
                );
                SELECT CAST(SCOPE_IDENTITY() AS BIGINT);";

            var vca = eventoBaseExtractorModelo.VCAModo.Info().Titulo;

            return await _dbConnection.ExecuteScalarAsync<long>(sql, new
            {
                CamaraIP = eventoBaseExtractorModelo.IP,
                CamaraMAC = eventoBaseExtractorModelo.MAC,
                VCA = vca,
                Evento = eventoBaseExtractorModelo.EventType,
                Prioridad,
                Payload,
                FechaEvento = eventoBaseExtractorModelo.Fecha,
            }, transaction: transaction);
        }
    }
}
