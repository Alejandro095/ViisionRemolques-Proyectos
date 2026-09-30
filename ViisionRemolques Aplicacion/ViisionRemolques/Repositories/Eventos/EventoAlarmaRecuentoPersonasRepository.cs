using Dapper;
using System.Data;
using System.Data.Common;
using ViisionRemolques.Entities;
using ViisionRemolques.Parsing.Extractors;
using ViisionRemolques.Parsing.Models;

namespace ViisionRemolques.Repositories.Eventos
{
    public class EventoAlarmaRecuentoPersonasRepository : EventoBaseRepository
    {
        private readonly ImagenesRepository _imagenesRepository;
        public EventoAlarmaRecuentoPersonasRepository(DbConnection dbConnection, ImagenesRepository imagenesRepository) : base(dbConnection)
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
                    INSERT INTO dbo.EventoDetallesAlarmaRecuentoPersonas (
                            EventoIdInterno,
                            ObjetivoDetectadoTipo,
                            Algoritmo,
                            RegionId,
                            RegionCoordenadas,
                            ValorCausaEvento,
                            OperadorCausaEvento,
                            CantidadPersonas,
                            NivelDensidad,
                            NombreNivelDensidad,
                            DireccionCambioDensidad
                        )
                        VALUES (
                            @EventoIdInterno,
                            @ObjetivoDetectadoTipo,
                            @Algoritmo,
                            @RegionId,
                            @RegionCoordenadas,
                            @ValorCausaEvento,
                            @OperadorCausaEvento,
                            @CantidadPersonas,
                            @NivelDensidad,
                            @NombreNivelDensidad,
                            @DireccionCambioDensidad
                        );";

                await _dbConnection.ExecuteAsync(sqlDetalle, eventoExtractorModelo.EventosAlarmaRecuentoPersonas.Select(eventoAlarmaRecuentoPersonas => new
                {
                    EventoIdInterno,
                    eventoAlarmaRecuentoPersonas.ObjetivoDetectadoTipo,
                    eventoAlarmaRecuentoPersonas.Algoritmo,
                    eventoAlarmaRecuentoPersonas.RegionId,
                    eventoAlarmaRecuentoPersonas.RegionCoordenadas,
                    eventoAlarmaRecuentoPersonas.ValorCausaEvento,
                    eventoAlarmaRecuentoPersonas.OperadorCausaEvento,
                    eventoAlarmaRecuentoPersonas.CantidadPersonas,
                    eventoAlarmaRecuentoPersonas.NivelDensidad,
                    eventoAlarmaRecuentoPersonas.NombreNivelDensidad,
                    eventoAlarmaRecuentoPersonas.DireccionCambioDensidad

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