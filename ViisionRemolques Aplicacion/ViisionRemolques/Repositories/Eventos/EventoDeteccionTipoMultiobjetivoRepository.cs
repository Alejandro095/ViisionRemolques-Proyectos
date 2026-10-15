using Dapper;
using System.Data;
using System.Data.Common;
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

            if (_dbConnection.State != ConnectionState.Open) await _dbConnection.OpenAsync();

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

        public async Task<List<EventoEntity<EventoDeteccionTipoMultiobjetivoEntity>>> ObtenerPendientesAsync()
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
                    d.Tipo,
                    d.Puntuacion,
                    d.HumanoEdad,
                    d.HumanoExpresionFacial,
                    d.HumanoColorChaqueta,
                    d.HumanoLentes,
                    d.HumanoGenero,
                    d.HumanoBolso,
                    d.HumanoSombrero,
                    d.HumanoTipoChaqueta,
                    d.HumanoMascarilla,
                    d.HumanoEstiloCabello,
                    d.HumanoGrupoEdad,
                    d.HumanoObjetos,
                    d.HumanoColorPantalon,
                    d.HumanoTipoPantalon,
                    d.HumanoDireccion,
                    d.HumanoDeteccionFacial,
                    d.HumanoDeteccionFacialId,
                    d.VehiculoMatricula,
                    d.VehiculoTipo,
                    d.VehiculoColor,
                    d.VehiculoLogo
                FROM dbo.Eventos e
                INNER JOIN dbo.EventoDetallesDeteccionTipoMultiobjetivo d ON d.EventoIdInterno = e.IdInterno
                WHERE e.IdExterno IS NULL
                ORDER BY e.IdInterno;";

            return await ObtenerPendientesBaseAsync<EventoDeteccionTipoMultiobjetivoEntity>(sql, splitOn: "Tipo");
        }
    }

    public class EventoDeteccionTipoMultiobjetivoEntity
    {
        public string? Tipo { get; set; }
        public string? Puntuacion { get; set; }

        public string? HumanoEdad { get; set; }
        public string? HumanoExpresionFacial { get; set; }
        public string? HumanoColorChaqueta { get; set; }
        public string? HumanoLentes { get; set; }
        public string? HumanoGenero { get; set; }
        public string? HumanoBolso { get; set; }
        public string? HumanoSombrero { get; set; }
        public string? HumanoTipoChaqueta { get; set; }
        public string? HumanoMascarilla { get; set; }
        public string? HumanoEstiloCabello { get; set; }
        public string? HumanoGrupoEdad { get; set; }
        public string? HumanoObjetos { get; set; }
        public string? HumanoColorPantalon { get; set; }
        public string? HumanoTipoPantalon { get; set; }
        public string? HumanoDireccion { get; set; }
        public bool? HumanoDeteccionFacial { get; set; }
        public string? HumanoDeteccionFacialId { get; set; }

        public string? VehiculoMatricula { get; set; }
        public string? VehiculoTipo { get; set; }
        public string? VehiculoColor { get; set; }
        public string? VehiculoLogo { get; set; }
    }
}