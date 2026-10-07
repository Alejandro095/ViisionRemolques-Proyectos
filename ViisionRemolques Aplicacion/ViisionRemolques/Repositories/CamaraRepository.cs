using Dapper;
using System.Data;
using System.Data.Common;
using ViisionRemolques.Enums;

namespace ViisionRemolques.Repositories
{
    public class CamaraRepository
    {
        private readonly DbConnection _dbConnection;

        public CamaraRepository(DbConnection dbConnection)
        {
            _dbConnection = dbConnection;
        }

        public async Task<IEnumerable<CamaraEntity>> ObtenerTodasAsync()
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
                    Soporta_AudioBidireccional,
                    Soporta_PtzMovimiento,
                    Soporta_PtzZoom,
                    Soporta_PtzEnfoque,
                    Soporta_PtzIris,
                    Soporta_PtzEscobilla,
                    Soporta_PtzEnfoqueAuxiliar,
                    Soporta_PtzInicializacionObjetivo,
                    Soporta_PtzCalibracionZoom
                FROM Camaras
                WHERE Activo = 1
                ORDER BY FechaCreacion DESC;";

            return await _dbConnection.QueryAsync<CamaraEntity>(sql);
        }

        public async Task<CamaraEntity?> ObtenerPorIdInternoAsync(long idInterno)
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
                    Soporta_AudioBidireccional,
                    Soporta_PtzMovimiento,
                    Soporta_PtzZoom,
                    Soporta_PtzEnfoque,
                    Soporta_PtzIris,
                    Soporta_PtzEscobilla,
                    Soporta_PtzEnfoqueAuxiliar,
                    Soporta_PtzInicializacionObjetivo,
                    Soporta_PtzCalibracionZoom
                FROM Camaras
                WHERE IdInterno = @IdInterno
                  AND Activo = 1;";

            return await _dbConnection.QueryFirstOrDefaultAsync<CamaraEntity>(sql, new { IdInterno = idInterno });
        }
    }

    public class CamaraEntity
    {
        public long IdInterno { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string Modelo { get; set; } = string.Empty;
        public PlataformaVcaEnum PlataformaVCA { get; set; }
        public string Go2Rtc { get; set; } = string.Empty;
        public bool Activo { get; set; }
        public string? IP { get; set; }

        // Digest
        public string? DigestUsuario { get; set; }
        public string? DigestContrasena { get; set; }

        // Funcionalidaes
        public bool Soporta_AudioBidireccional { get; set; }

        public bool Soporta_PtzMovimiento { get; set; }
        public bool Soporta_PtzZoom { get; set; }
        public bool Soporta_PtzEnfoque { get; set; }
        public bool Soporta_PtzIris { get; set; }
        public bool Soporta_PtzEscobilla { get; set; }
        public bool Soporta_PtzEnfoqueAuxiliar { get; set; }
        public bool Soporta_PtzInicializacionObjetivo { get; set; }
        public bool Soporta_PtzCalibracionZoom { get; set; }
    }
}
