using Dapper;
using System.Data;
using System.Data.Common;
using ViisionRemolques.Parsing.Models;

namespace ViisionRemolques.Repositories.Eventos
{
    public class EventoRecuentoPersonasRepository : EventoBaseRepository
    {
        private readonly ImagenesRepository _imagenesRepository;
        public EventoRecuentoPersonasRepository(DbConnection dbConnection, ImagenesRepository imagenesRepository) : base(dbConnection)
        {
            _imagenesRepository = imagenesRepository;
        }

        public async Task InsertarAsync(
            EventoExtractorModelo eventoExtractorModelo, 
            List<string>? ImagenesPaths = null,
            string? Payload = null
        ) 
        {
            ImagenesPaths ??= new List<string>();

            if (_dbConnection.State != ConnectionState.Open) await _dbConnection.OpenAsync();

            await using var transaction = await _dbConnection.BeginTransactionAsync();

            try
            {
                // Insertar cabecera
                var EventoIdInterno = await this.InsertarEventoBaseAsync(eventoExtractorModelo.Evento, Payload: Payload, transaction: transaction);
                
                // Insertar detalles
                const string sqlDetalle = @"
                    INSERT INTO dbo.EventoDetallesRecuentoPersonas (
                            EventoIdInterno,
                            TodasRegionesEntradas,
                            TodasRegionesSalidas,
                            TodasRegionesTranseuntes,
                            TodasRegionesDuplicados,
                            RegionId,
                            RegionEntradas,
                            RegionSalidas,
                            RegionTranseuntes
                        )
                        VALUES (
                            @EventoIdInterno,
                            @TodasRegionesEntradas,
                            @TodasRegionesSalidas,
                            @TodasRegionesTranseuntes,
                            @TodasRegionesDuplicados,
                            @RegionId,
                            @RegionEntradas,
                            @RegionSalidas,
                            @RegionTranseuntes
                        );";

                await _dbConnection.ExecuteAsync(sqlDetalle, eventoExtractorModelo.EventosRecuentoPersonas.Select(eventoRecuentoPersonas => new
                {
                    EventoIdInterno,
                    eventoRecuentoPersonas.TodasRegionesEntradas,
                    eventoRecuentoPersonas.TodasRegionesSalidas,
                    eventoRecuentoPersonas.TodasRegionesTranseuntes,
                    eventoRecuentoPersonas.TodasRegionesDuplicados,
                    eventoRecuentoPersonas.RegionId,
                    eventoRecuentoPersonas.RegionEntradas,
                    eventoRecuentoPersonas.RegionSalidas,
                    eventoRecuentoPersonas.RegionTranseuntes

                }), transaction: transaction);

                // Insertar imagenes
                await _imagenesRepository.InsertarAsync(EventoIdInterno, ImagenesPaths, transaction);

                transaction.Commit();

            } catch
            {
                transaction.Rollback();
                throw;
            }
        }

        public async Task<List<EventoEntity<EventoRecuentoPersonasEntity>>> ObtenerPendientesAsync()
        {
            const string sql = @"
                SELECT
                    e.IdInterno AS EventoIdInterno,
                    e.CamaraIP,
                    e.CamaraMAC,
                    e.VCA,
                    e.Evento,
                    e.Prioridad,
                    e.Payload,
                    e.FechaEvento,
                    d.TodasRegionesEntradas,
                    d.TodasRegionesSalidas,
                    d.TodasRegionesTranseuntes,
                    d.TodasRegionesDuplicados,
                    d.RegionId,
                    d.RegionEntradas,
                    d.RegionSalidas,
                    d.RegionTranseuntes
                FROM dbo.Eventos e
                INNER JOIN dbo.EventoDetallesRecuentoPersonas d ON d.EventoIdInterno = e.IdInterno
                WHERE e.IdExterno IS NULL
                ORDER BY e.IdInterno;";

            return await ObtenerPendientesBaseAsync<EventoRecuentoPersonasEntity>(sql, splitOn: "TodasRegionesEntradas");
        }
    }

    public class EventoRecuentoPersonasEntity
    {
        public string? TodasRegionesEntradas { get; set; }
        public string? TodasRegionesSalidas { get; set; }
        public string? TodasRegionesTranseuntes { get; set; }
        public string? TodasRegionesDuplicados { get; set; }
        public string? RegionId { get; set; }
        public string? RegionEntradas { get; set; }
        public string? RegionSalidas { get; set; }
        public string? RegionTranseuntes { get; set; }
    }
}