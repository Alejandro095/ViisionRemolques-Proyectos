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

        protected async Task<List<EventoEntity<TDetalle>>> ObtenerPendientesBaseAsync<TDetalle>(string sql, string splitOn)
        {
            if (_dbConnection.State != ConnectionState.Open) await _dbConnection.OpenAsync();

            var pendientes = new Dictionary<long, EventoEntity<TDetalle>>();

            await _dbConnection.QueryAsync<EventoEntity<TDetalle>, TDetalle, EventoEntity<TDetalle>>(
                sql,
                (evento, detalle) =>
                {
                    if (!pendientes.TryGetValue(evento.EventoIdInterno, out var existente))
                    {
                        existente = evento;
                        pendientes[evento.EventoIdInterno] = existente;
                    }

                    existente.Detalles.Add(detalle);

                    return existente;
                },
                splitOn: splitOn);

            return pendientes.Values.ToList();
        }

        public async Task<int> MarcarComoSincronizadoAsync(long eventoIdInterno, long idExterno, IDbTransaction? transaction = null)
        {
            if (_dbConnection.State != ConnectionState.Open) await _dbConnection.OpenAsync();

            const string sql = @"
                UPDATE dbo.Eventos
                SET IdExterno = @IdExterno
                WHERE IdInterno = @EventoIdInterno;";

            return await _dbConnection.ExecuteAsync(sql, new
            {
                EventoIdInterno = eventoIdInterno,
                IdExterno = idExterno
            }, transaction: transaction);
        }
    }

    public class EventoEntity<TDetalle>
    {
        public long EventoIdInterno { get; set; }
        public string CamaraIP { get; set; } = string.Empty;
        public string CamaraMAC { get; set; } = string.Empty;
        public string VCA { get; set; } = string.Empty;
        public string Evento { get; set; } = string.Empty;
        public int Prioridad { get; set; }
        public string? Payload { get; set; }
        public DateTime FechaEvento { get; set; }
        public List<TDetalle> Detalles { get; set; } = new();
    }
}
