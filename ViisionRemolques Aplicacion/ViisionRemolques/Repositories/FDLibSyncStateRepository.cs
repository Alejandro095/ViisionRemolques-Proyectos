using System.Data;
using System.Data.Common;
using ViisionRemolques.Utils;
using Dapper;

namespace ViisionRemolques.Repositories
{
    public class FDLibSyncStateRepository
    {
        private readonly DbConnection _dbConnection;

        public FDLibSyncStateRepository(DbConnection dbConnection)
        {
            _dbConnection = dbConnection;
        }

        public record FDLibPendiente(long ImagenIdExterno, long CiudadanoIdExterno, string Accion, string? Pid, int IntentosFallidos, string? RutaArchivoLocal, string? Hash, string? Nombre, string? ApellidoPaterno, string? ApellidoMaterno);
        public async Task<Resultado<IEnumerable<FDLibPendiente>>> ObtenerDiferencias()
        {
            const string sql = @"
            SELECT
                s.ImagenIdExterno,
                s.CiudadanoIdExterno,
                s.Accion,
                s.Pid,
                s.IntentosFallidos,
                pf.RutaArchivoLocal,
                pf.Hash,
                c.Nombre,
                c.ApellidoPaterno,
                c.ApellidoMaterno
            FROM FDLibSyncState s
            LEFT JOIN CiudadanosReportadosFotosEstado pf ON pf.ImagenIdExterno = s.ImagenIdExterno
            LEFT JOIN CiudadanosReportados c ON c.IdExterno = s.CiudadanoIdExterno
            WHERE s.Estado = 'Pendiente';";

            try
            {
                if (_dbConnection.State != ConnectionState.Open) await _dbConnection.OpenAsync();

                var filas = await _dbConnection.QueryAsync<FDLibPendiente>(sql);

                return Resultado<IEnumerable<FDLibPendiente>>.Ok(filas);
            }
            catch (Exception ex)
            {
                return Resultado<IEnumerable<FDLibPendiente>>.Fallo(ex);
            }
        }

        public async Task<Resultado> ProcesarDiferencias()
        {
            const string sql = @"
                MERGE FDLibSyncState AS destino
                USING (
                    -- Borrar / LimpiarLocal
                    SELECT
                        s.ImagenIdExterno,
                        s.CiudadanoIdExterno,
                        CASE WHEN s.Pid IS NOT NULL THEN 'Borrar' ELSE 'LimpiarLocal' END AS Accion
                    FROM FDLibSyncState s
                    LEFT JOIN CiudadanosReportadosFotosEstado pf ON pf.ImagenIdExterno = s.ImagenIdExterno
                    WHERE pf.ImagenIdExterno IS NULL

                    UNION ALL

                    -- Insertar / Actualizar
                    SELECT
                        pf.ImagenIdExterno,
                        pf.CiudadanoIdExterno,
                        CASE WHEN s.ImagenIdExterno IS NULL THEN 'Insertar' ELSE 'Actualizar' END
                    FROM CiudadanosReportadosFotosEstado pf
                    LEFT JOIN FDLibSyncState s ON s.ImagenIdExterno = pf.ImagenIdExterno
                    WHERE s.ImagenIdExterno IS NULL OR pf.Hash <> s.HashContenido
                ) AS origen ON destino.ImagenIdExterno = origen.ImagenIdExterno
                WHEN MATCHED AND origen.Accion = 'LimpiarLocal' THEN DELETE
                WHEN MATCHED THEN UPDATE SET
                    Accion = origen.Accion, Estado = 'Pendiente', FechaActualizacion = SYSUTCDATETIME()
                WHEN NOT MATCHED BY TARGET THEN
                    INSERT (ImagenIdExterno, CiudadanoIdExterno, Accion, Estado, FechaActualizacion)
                    VALUES (origen.ImagenIdExterno, origen.CiudadanoIdExterno, origen.Accion, 'Pendiente', SYSUTCDATETIME());";

            try
            {
                if (_dbConnection.State != ConnectionState.Open) await _dbConnection.OpenAsync();

                await _dbConnection.ExecuteAsync(sql);

                return Resultado.Ok();
            }
            catch (Exception ex)
            {
                return Resultado.Fallo(ex);
            }
        }

        public record FDLibExito(long ImagenIdExterno, string Pid, string Hash);
        public record FDLibError(long ImagenIdExterno, string UltimoError);

        public async Task<Resultado> MarcarSincronizadoAsync(FDLibExito fila)
        {
            const string sql = @"
                UPDATE FDLibSyncState SET
                    Estado = 'Sincronizado', Pid = @Pid, HashContenido = @Hash,
                    IntentosFallidos = 0, UltimoError = NULL, FechaActualizacion = SYSUTCDATETIME()
                WHERE ImagenIdExterno = @ImagenIdExterno;";

            try
            {
                if (_dbConnection.State != ConnectionState.Open) await _dbConnection.OpenAsync();

                await _dbConnection.ExecuteAsync(sql, fila);

                return Resultado.Ok();
            }
            catch (Exception ex)
            {
                return Resultado.Fallo(ex);
            }
        }

        public async Task<Resultado> EliminarEstadoCamaraAsync(long imagenIdExterno)
        {
            try
            {
                if (_dbConnection.State != ConnectionState.Open) await _dbConnection.OpenAsync();

                await _dbConnection.ExecuteAsync(
                    "DELETE FROM FDLibSyncState WHERE ImagenIdExterno = @imagenIdExterno",
                    new { imagenIdExterno });

                return Resultado.Ok();
            }
            catch (Exception ex)
            {
                return Resultado.Fallo(ex);
            }
        }

        public async Task<Resultado> MarcarErrorCamaraAsync(FDLibError fila)
        {
            const string sql = @"
                UPDATE FDLibSyncState SET
                    Estado = 'Error', IntentosFallidos = IntentosFallidos + 1,
                    UltimoError = @UltimoError, FechaActualizacion = SYSUTCDATETIME()
                WHERE ImagenIdExterno = @ImagenIdExterno;";

            try
            {
                if (_dbConnection.State != ConnectionState.Open) await _dbConnection.OpenAsync();

                await _dbConnection.ExecuteAsync(sql, fila);

                return Resultado.Ok();
            }
            catch (Exception ex)
            {
                return Resultado.Fallo(ex);
            }
        }
    }
}
