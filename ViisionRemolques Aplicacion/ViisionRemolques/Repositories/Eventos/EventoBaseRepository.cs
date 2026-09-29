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

            var vca = eventoBaseExtractorModelo.VCAModo.Val();

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

    //public class EventoBaseEntity
    //{
    //    public long IdInterno { get; set; }
    //    public long? IdExterno { get; set; }
    //    public string? CamaraIP { get; set; }
    //    public string? CamaraMAC { get; set; }
    //    public string? Evento {  get; set; }
    //    public int Prioridad { get; set; } = 5;
    //    public string? TablaExterna { get; set; }
    //    public string? ReferenciaExterna { get; set; }
    //    public DateTime FechaEvento { get; set; } = DateTime.Now;
    //    public string? Payload { get; set; }
    //}
}
