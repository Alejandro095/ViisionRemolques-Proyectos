using Dapper;
using System.Data;
using System.Data.Common;
using ViisionRemolques.Entities;
using ViisionRemolques.Parsing.Extractors;
using ViisionRemolques.Parsing.Models;

namespace ViisionRemolques.Repositories.Eventos
{
    public class EventoDeteccionTipoMultiobjetivoRepository : EventoBaseRepository
    {
        private readonly ImagenesRepository _imagenesRepository;
        public EventoDeteccionTipoMultiobjetivoRepository(DbConnection dbConnection, ImagenesRepository imagenesRepository) : base(dbConnection)
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
                    INSERT INTO dbo.EventoDetallesDeteccionTipoMultiobjetivo (
                            EventoIdInterno,
                            Tipo,
                            Puntuacion,
                            HumanoEdad,
                            HumanoExpresionFacial,
                            HumanoColorChaqueta,
                            HumanoLentes,
                            HumanoGenero,
                            HumanoBolso,
                            HumanoSombrero,
                            HumanoTipoChaqueta,
                            HumanoMascarilla,
                            HumanoEstiloCabello,
                            HumanoGrupoEdad,
                            HumanoObjetos,
                            HumanoColorPantalon,
                            HumanoTipoPantalon,
                            HumanoDireccion,
                            HumanoDeteccionFacial,
                            HumanoDeteccionFacialId,
                            VehiculoMatricula,
                            VehiculoTipo,
                            VehiculoColor,
                            VehiculoLogo
                        )
                        VALUES (
                            @EventoIdInterno,
                            @Tipo,
                            @Puntuacion,
                            @HumanoEdad,
                            @HumanoExpresionFacial,
                            @HumanoColorChaqueta,
                            @HumanoLentes,
                            @HumanoGenero,
                            @HumanoBolso,
                            @HumanoSombrero,
                            @HumanoTipoChaqueta,
                            @HumanoMascarilla,
                            @HumanoEstiloCabello,
                            @HumanoGrupoEdad,
                            @HumanoObjetos,
                            @HumanoColorPantalon,
                            @HumanoTipoPantalon,
                            @HumanoDireccion,
                            @HumanoDeteccionFacial,
                            @HumanoDeteccionFacialId,                            
                            @VehiculoMatricula,
                            @VehiculoTipo,
                            @VehiculoColor,
                            @VehiculoLogo
                        );";

                await _dbConnection.ExecuteAsync(sqlDetalle, eventoExtractorModelo.EventosDeteccionTipoMultiobjectivo.Select(eventoDeteccionTipoMultiobjectivo => new
                {
                    EventoIdInterno,
                    eventoDeteccionTipoMultiobjectivo.Tipo,
                    Puntuacion = eventoDeteccionTipoMultiobjectivo.Humano?.Puntuacion ?? eventoDeteccionTipoMultiobjectivo.Vehiculo?.Puntuacion,

                    HumanoEdad = eventoDeteccionTipoMultiobjectivo.Humano?.Edad,
                    HumanoExpresionFacial = eventoDeteccionTipoMultiobjectivo.Humano?.ExpresionFacial,
                    HumanoColorChaqueta = eventoDeteccionTipoMultiobjectivo.Humano?.ColorChaqueta,
                    HumanoLentes = eventoDeteccionTipoMultiobjectivo.Humano?.Lentes,
                    HumanoGenero = eventoDeteccionTipoMultiobjectivo.Humano?.Genero,
                    HumanoBolso = eventoDeteccionTipoMultiobjectivo.Humano?.Bolso,
                    HumanoSombrero = eventoDeteccionTipoMultiobjectivo.Humano?.Sombrero,
                    HumanoTipoChaqueta = eventoDeteccionTipoMultiobjectivo.Humano?.TipoChaqueta,
                    HumanoMascarilla = eventoDeteccionTipoMultiobjectivo.Humano?.Mascarilla,
                    HumanoEstiloCabello = eventoDeteccionTipoMultiobjectivo.Humano?.EstiloCabello,
                    HumanoGrupoEdad = eventoDeteccionTipoMultiobjectivo.Humano?.GrupoEdad,
                    HumanoObjetos = eventoDeteccionTipoMultiobjectivo.Humano?.Objetos,
                    HumanoColorPantalon = eventoDeteccionTipoMultiobjectivo.Humano?.ColorPantalon,
                    HumanoTipoPantalon = eventoDeteccionTipoMultiobjectivo.Humano?.TipoPantalon,
                    HumanoDireccion = eventoDeteccionTipoMultiobjectivo.Humano?.Direcion,
                    HumanoDeteccionFacial = eventoDeteccionTipoMultiobjectivo.Humano?.DeteccionFacial,
                    HumanoDeteccionFacialId = eventoDeteccionTipoMultiobjectivo.Humano?.DeteccionFacialId,

                    VehiculoMatricula = eventoDeteccionTipoMultiobjectivo.Vehiculo?.Matricula,
                    VehiculoTipo = eventoDeteccionTipoMultiobjectivo.Vehiculo?.Tipo,
                    VehiculoColor = eventoDeteccionTipoMultiobjectivo.Vehiculo?.Color,
                    VehiculoLogo = eventoDeteccionTipoMultiobjectivo.Vehiculo?.Logo

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