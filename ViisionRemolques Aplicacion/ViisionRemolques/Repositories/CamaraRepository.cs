using Dapper;
using System.Data;
using System.Data.Common;
using ViisionRemolques.Entities;

namespace ViisionRemolques.Repositories
{
    public class CamaraRepository
    {
        private readonly DbConnection _dbConnection;

        public CamaraRepository(DbConnection dbConnection)
        {
            _dbConnection = dbConnection;
        }

        public async Task<IEnumerable<Camara>> ObtenerTodasAsync()
        {
            const string sql = @"
                SELECT
                    IdInterno,
                    Nombre,
                    Modelo,
                    Activo,
                    IP,
                    Go2Rtc,
                    PlataformaVCA,
                    Digest_Usuario,
                    Digest_Contrasena,
                    Soporta_PTZ,
                    SoportaAudioBidireccional,
                    EventoSmart_DeteccionIntrusiones,
                    EventoSmart_DeteccionCruceLinea,
                    EventoSmart_DeteccionEntradaArea,
                    EventoSmart_DeteccionSalidaArea,
                    EventoSmart_EventoCombinado
                FROM Camaras
                WHERE Activo = 1
                ORDER BY FechaCreacion DESC;";

            return await _dbConnection.QueryAsync<Camara>(sql);
        }

        public async Task<Camara?> ObtenerPorIdInternoAsync(long idInterno)
        {
            const string sql = @"
                SELECT TOP 1
                    IdInterno,
                    Nombre,
                    Modelo,
                    Activo,
                    IP,
                    Go2Rtc,
                    PlataformaVCA,
                    Digest_Usuario,
                    Digest_Contrasena,
                    Soporta_PTZ,
                    SoportaAudioBidireccional,
                    EventoSmart_DeteccionIntrusiones,
                    EventoSmart_DeteccionCruceLinea,
                    EventoSmart_DeteccionEntradaArea,
                    EventoSmart_DeteccionSalidaArea,
                    EventoSmart_EventoCombinado
                FROM Camaras
                WHERE IdInterno = @IdInterno
                  AND Activo = 1;";

            return await _dbConnection.QueryFirstOrDefaultAsync<Camara>(sql, new { IdInterno = idInterno });
        }
    }
}
