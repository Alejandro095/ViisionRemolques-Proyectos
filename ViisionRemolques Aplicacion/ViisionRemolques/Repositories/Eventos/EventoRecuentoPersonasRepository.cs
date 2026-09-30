using Dapper;
using System.Data;
using System.Data.Common;
using ViisionRemolques.Entities;
using ViisionRemolques.Parsing.Extractors;
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

            await _dbConnection.OpenAsync();

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
    }
}