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

        public async Task<Resultado<IEnumerable<FDLibPendiente>>> ObtenerDiferenciasProcesadas()
        {
            var result = await ProcesarDiferencias();

            if (!result.Exito) return Resultado<IEnumerable<FDLibPendiente>>.Fallo(result.Error!);

            return await ObtenerDiferencias();
        }

        public record FDLibSincronizado(long ImagenIdExterno, long CiudadanoIdExterno);
        public async Task<Resultado<IEnumerable<FDLibSincronizado>>> ObtenerSincronizadosAsync()
        {
            const string sql = @"SELECT ImagenIdExterno, CiudadanoIdExterno FROM FDLibSyncState WHERE Sincronizado = 1;";

            try
            {
                if (_dbConnection.State != ConnectionState.Open) await _dbConnection.OpenAsync();

                var filas = await _dbConnection.QueryAsync<FDLibSincronizado>(sql);

                return Resultado<IEnumerable<FDLibSincronizado>>.Ok(filas);
            }
            catch (Exception ex)
            {
                return Resultado<IEnumerable<FDLibSincronizado>>.Fallo(ex);
            }
        }

        public record FDLibPendiente(long ImagenIdExterno, long CiudadanoIdExterno, string Accion, bool Sincronizado, int IntentosFallidos, string? RutaArchivoLocal, string? Hash, string? Nombre, string? ApellidoPaterno, string? ApellidoMaterno);
        public async Task<Resultado<IEnumerable<FDLibPendiente>>> ObtenerDiferencias()
        {
            const string sql = @"
            SELECT
                s.ImagenIdExterno,
                s.CiudadanoIdExterno,
                s.Accion,
                s.Sincronizado,
                s.IntentosFallidos,
                pf.RutaArchivoLocal,
                pf.Hash,
                c.Nombre,
                c.ApellidoPaterno,
                c.ApellidoMaterno
            FROM FDLibSyncState s
            LEFT JOIN CiudadanosReportadosFotosEstado pf ON pf.ImagenIdExterno = s.ImagenIdExterno
            LEFT JOIN CiudadanosReportados c ON c.IdExterno = s.CiudadanoIdExterno
            WHERE s.Estado != 'Sincronizado';";

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
                        CASE WHEN s.Sincronizado = 1 THEN 'Borrar' ELSE 'LimpiarLocal' END AS Accion
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
                    Accion = origen.Accion, Estado = 'Pendiente', FechaActualizacion = GETDATE()
                WHEN NOT MATCHED BY TARGET THEN
                    INSERT (ImagenIdExterno, CiudadanoIdExterno, Accion, Estado, FechaActualizacion)
                    VALUES (origen.ImagenIdExterno, origen.CiudadanoIdExterno, origen.Accion, 'Pendiente', GETDATE());";

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

        public record FDLibExito(long ImagenIdExterno, string Hash);
        public record FDLibError(long ImagenIdExterno, string UltimoError);

        public async Task<Resultado> MarcarSincronizadoAsync(FDLibExito fila)
        {
            const string sql = @"
                UPDATE FDLibSyncState SET
                    Estado = 'Sincronizado', Sincronizado = 1, HashContenido = @Hash,
                    IntentosFallidos = 0, UltimoError = NULL, FechaActualizacion = GETDATE()
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
                    UltimoError = @UltimoError, FechaActualizacion = GETDATE()
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
