using Dapper;
using System.Data.Common;
using ViisionRemolques.Parsing.Models;

namespace ViisionRemolques.Repositories.Eventos
{
    public class EventoTraficoRodadoRepository : EventoBaseRepository
    {
        private readonly ImagenesRepository _imagenesRepository;
        public EventoTraficoRodadoRepository(DbConnection dbConnection, ImagenesRepository imagenesRepository) : base(dbConnection)
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
                    INSERT INTO dbo.EventoDetallesTraficoRodado (
                            EventoIdInterno,
                            VehiculosTotal,
                            VehiculosFlujoAscendente,
                            VehiculosFlujoDescendente,
                            MotocicletasTotal,
                            MotocicletasFlujoAscendente,
                            MotocicletasFlujoDescendente
                        )
                        VALUES (
                            @EventoIdInterno,
                            @VehiculosTotal,
                            @VehiculosFlujoAscendente,
                            @VehiculosFlujoDescendente,
                            @MotocicletasTotal,
                            @MotocicletasFlujoAscendente,
                            @MotocicletasFlujoDescendente
                        );";

                await _dbConnection.ExecuteAsync(sqlDetalle, new
                {
                    EventoIdInterno,
                    eventoExtractorModelo.EventoTraficoRodado?.VehiculosTotal,
                    eventoExtractorModelo.EventoTraficoRodado?.VehiculosFlujoAscendente,
                    eventoExtractorModelo.EventoTraficoRodado?.VehiculosFlujoDescendente,
                    eventoExtractorModelo.EventoTraficoRodado?.MotocicletasTotal,
                    eventoExtractorModelo.EventoTraficoRodado?.MotocicletasFlujoAscendente,
                    eventoExtractorModelo.EventoTraficoRodado?.MotocicletasFlujoDescendente
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