using Dapper;
using System.Data;
using System.Data.Common;
using ViisionRemolques.Parsing.Models;

namespace ViisionRemolques.Repositories.Eventos
{
    public class EventoSmartRepository: EventoBaseRepository
    {
        private readonly ImagenesRepository _imagenesRepository;
        public EventoSmartRepository(DbConnection dbConnection, ImagenesRepository imagenesRepository) : base(dbConnection)
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
                    INSERT INTO dbo.EventoDetallesSmart (
                            EventoIdInterno,
                            RegionId,
                            ObjetivoDetectadoTipo,
                            RegionCoordenadas
                        )
                        VALUES (
                            @EventoIdInterno,
                            @RegionId,
                            @ObjetivoDetectadoTipo,
                            @RegionCoordenadas
                        );";

                await _dbConnection.ExecuteAsync(sqlDetalle, eventoExtractorModelo.EventosSmart.Select(eventoSmart => new
                {
                    EventoIdInterno,
                    eventoSmart.RegionId,
                    eventoSmart.ObjetivoDetectadoTipo,
                    eventoSmart.RegionCoordenadas

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
    }
}