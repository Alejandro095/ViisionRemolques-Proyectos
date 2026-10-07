using Dapper;
using System.Data;
using System.Data.Common;
using ViisionRemolques.Parsing.Models;

namespace ViisionRemolques.Repositories.Eventos
{
    public class EventoArmadoPistaPersonaRepository : EventoBaseRepository
    {
        private readonly ImagenesRepository _imagenesRepository;
        public EventoArmadoPistaPersonaRepository(DbConnection dbConnection, ImagenesRepository imagenesRepository) : base(dbConnection)
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
                    INSERT INTO dbo.EventoDetallesArmadoPistaPersona (
                            EventoIdInterno,
                            Edad,
                            GrupoEdad,
                            Genero,
                            Lentes,
                            Mascara,
                            ExpresionFacial,
                            Sombrero,
                            DeteccionFacial,
                            DeteccionFacialId
                        )
                        VALUES (
                            @EventoIdInterno,
                            @Edad,
                            @GrupoEdad,
                            @Genero,
                            @Lentes,
                            @Mascara,
                            @ExpresionFacial,
                            @Sombrero,
                            @DeteccionFacial,
                            @DeteccionFacialId
                        );";

                await _dbConnection.ExecuteAsync(sqlDetalle, eventoExtractorModelo.EventosArmadoPistaPersona.Select(eventoArmadoPistaPersona => new
                {
                    EventoIdInterno,
                    eventoArmadoPistaPersona.Edad,
                    eventoArmadoPistaPersona.GrupoEdad,
                    eventoArmadoPistaPersona.Genero,
                    eventoArmadoPistaPersona.Lentes,
                    eventoArmadoPistaPersona.Mascara,
                    eventoArmadoPistaPersona.ExpresionFacial,
                    eventoArmadoPistaPersona.Sombrero,
                    eventoArmadoPistaPersona.DeteccionFacial,
                    eventoArmadoPistaPersona.DeteccionFacialId
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