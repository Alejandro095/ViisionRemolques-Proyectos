using Dapper;
using System.Data;
using System.Data.Common;
using ViisionRemolques.Parsing.Models;

namespace ViisionRemolques.Repositories.Eventos
{
    public class EventoANPRRepository : EventoBaseRepository
    {
        private readonly ImagenesRepository _imagenesRepository;
        public EventoANPRRepository(DbConnection dbConnection, ImagenesRepository imagenesRepository) : base(dbConnection)
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
                    INSERT INTO dbo.EventoDetallesANPR (
                            EventoIdInterno,
                            Matricula,
                            VehiculoDosRuedas,
                            VehiculoTresRuedas,
                            VehiculoTipo,
                            VehiculoColor,
                            Direccion,
                            NumeroCarril,
                            NombreLista,
                            Radar,
                            Velocidad
                        )
                        VALUES (
                            @EventoIdInterno,
                            @Matricula,
                            @VehiculoDosRuedas,
                            @VehiculoTresRuedas,
                            @VehiculoTipo,
                            @VehiculoColor,
                            @Direccion,
                            @NumeroCarril,
                            @NombreLista,
                            @Radar,
                            @Velocidad
                        );";

                await _dbConnection.ExecuteAsync(sqlDetalle, new
                {
                    EventoIdInterno,
                    eventoExtractorModelo.EventoANPR.Matricula,
                    eventoExtractorModelo.EventoANPR.VehiculoDosRuedas,
                    eventoExtractorModelo.EventoANPR.VehiculoTresRuedas,
                    eventoExtractorModelo.EventoANPR.VehiculoTipo,
                    eventoExtractorModelo.EventoANPR.VehiculoColor,
                    eventoExtractorModelo.EventoANPR.Direccion,
                    eventoExtractorModelo.EventoANPR.NumeroCarril,
                    eventoExtractorModelo.EventoANPR.NombreLista,
                    eventoExtractorModelo.EventoANPR.Radar,
                    eventoExtractorModelo.EventoANPR.Velocidad

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

        public async Task<List<EventoEntity<EventoANPREntity>>> ObtenerPendientesAsync()
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
                    d.Matricula,
                    d.VehiculoDosRuedas,
                    d.VehiculoTresRuedas,
                    d.VehiculoTipo,
                    d.VehiculoColor,
                    d.Direccion,
                    d.NumeroCarril,
                    d.NombreLista,
                    d.Radar,
                    d.Velocidad
                FROM dbo.Eventos e
                INNER JOIN dbo.EventoDetallesANPR d ON d.EventoIdInterno = e.IdInterno
                WHERE e.IdExterno IS NULL
                ORDER BY e.IdInterno;";

            return await ObtenerPendientesBaseAsync<EventoANPREntity>(sql, splitOn: "Matricula");
        }
    }

    public class EventoANPREntity
    {
        public string? Matricula { get; set; }
        public string? VehiculoDosRuedas { get; set; }
        public string? VehiculoTresRuedas { get; set; }
        public string? VehiculoTipo { get; set; }
        public string? VehiculoColor { get; set; }
        public string? Direccion { get; set; }
        public string? NumeroCarril { get; set; }
        public string? NombreLista { get; set; }
        public bool Radar { get; set; }
        public string? Velocidad { get; set; }
    }
}