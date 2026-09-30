using Dapper;
using System.Data;
using System.Data.Common;
using ViisionRemolques.Entities;
using ViisionRemolques.Parsing.Extractors;
using ViisionRemolques.Parsing.Models;

namespace ViisionRemolques.Repositories.Eventos
{
    public class EventoCapturaFacialRepository : EventoBaseRepository
    {
        private readonly ImagenesRepository _imagenesRepository;
        public EventoCapturaFacialRepository(DbConnection dbConnection, ImagenesRepository imagenesRepository) : base(dbConnection)
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

            await _dbConnection.OpenAsync();

            await using var transaction = await _dbConnection.BeginTransactionAsync();

            try
            {
                // Insertar cabecera
                var EventoIdInterno = await this.InsertarEventoBaseAsync(eventoExtractorModelo.Evento, Payload: Payload, transaction: transaction);
                
                // Insertar detalles
                const string sqlDetalle = @"
                    INSERT INTO dbo.EventoDetallesCapturaFacial (
                            EventoIdInterno,
                            RegionCoordenadas
                        )
                        VALUES (
                            @EventoIdInterno,
                            @RegionCoordenadas
                        );";

                await _dbConnection.ExecuteAsync(sqlDetalle, new
                {
                    EventoIdInterno,
                    eventoExtractorModelo.EventoCapturaFacial.RegionCoordenadas

                }, transaction: transaction);

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