using Dapper;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Data.Common;
using ViisionRemolques.Services.Centralia.Endpoints;
using ViisionRemolques.Utils;

namespace ViisionRemolques.Repositories
{
    public class CiudadanosRepository
    {
        private readonly DbConnection _dbConnection;

        public CiudadanosRepository(DbConnection dbConnection)
        {
            _dbConnection = dbConnection;
        }

        public async Task<Resultado> Actualizar(List<CiudadanoReportado> ciudadanosReportados)
        {
            ciudadanosReportados ??= new List<CiudadanoReportado>();

            using var dataTable = new DataTable();

            dataTable.Columns.Add("IdExterno", typeof(long));
            dataTable.Columns.Add("Nombre", typeof(string));
            dataTable.Columns.Add("ApellidoPaterno", typeof(string));
            dataTable.Columns.Add("ApellidoMaterno", typeof(string));
            dataTable.Columns.Add("Sexo", typeof(string));
            dataTable.Columns.Add("FechaNacimiento", typeof(DateTime));
            dataTable.Columns.Add("RFC", typeof(string));
            dataTable.Columns.Add("Curp", typeof(string));
            dataTable.Columns.Add("IFE", typeof(string));
            dataTable.Columns.Add("Mensaje", typeof(string));
            dataTable.Columns.Add("Accion", typeof(string));
            dataTable.Columns.Add("AltoRiesgo", typeof(bool));
            dataTable.Columns.Add("Activo", typeof(bool));
            dataTable.Columns.Add("FechaCreacion", typeof(DateTime));
            dataTable.Columns.Add("FechaModificacion", typeof(DateTime));

            foreach (var item in ciudadanosReportados)
            {
                if (item.IdExterno is null) continue;

                dataTable.Rows.Add(
                    item.IdExterno,
                    (object?)item.Nombre ?? DBNull.Value,
                    (object?)item.ApellidoPaterno ?? DBNull.Value,
                    (object?)item.ApellidoMaterno ?? DBNull.Value,
                    (object?)item.Sexo ?? DBNull.Value,
                    (object?)item.FechaNacimiento ?? DBNull.Value,
                    (object?)item.RFC ?? DBNull.Value,
                    (object?)item.Curp ?? DBNull.Value,
                    (object?)item.IFE ?? DBNull.Value,
                    (object?)item.Mensaje ?? DBNull.Value,
                    (object?)item.Accion ?? DBNull.Value,
                    item.AltoRiesgo,
                    item.Activo,
                    (object?)item.FechaCreacion ?? DBNull.Value,
                    (object?)item.FechaModificacion ?? DBNull.Value
                );
            }

            return await EjecutarCargaMasivaAsync("CiudadanosReportados", dataTable);
        }

        public async Task<Resultado> ActualizarImagenes(List<CiudadanoReportadoImagenDetalleRespuesta>? ciudadanosReportadosImagenes)
        {
            ciudadanosReportadosImagenes ??= new List<CiudadanoReportadoImagenDetalleRespuesta>();

            using var dataTable = new DataTable();
            dataTable.Columns.Add("IdExterno", typeof(long));
            dataTable.Columns.Add("CiudadanoIdExterno", typeof(long));
            dataTable.Columns.Add("Path", typeof(string));
            dataTable.Columns.Add("Hash", typeof(string));
            dataTable.Columns.Add("Activo", typeof(bool));
            dataTable.Columns.Add("FechaCreacion", typeof(DateTime));
            dataTable.Columns.Add("FechaModificacion", typeof(DateTime));

            foreach (var item in ciudadanosReportadosImagenes)
            {
                if (item.IdExterno is null) continue;
                if (item.CiudadanoIdExterno is null) continue;
                if (item.Path is null) continue;
                if (item.Hash is null) continue;

                dataTable.Rows.Add(
                    item.IdExterno,
                    item.CiudadanoIdExterno,
                    item.Path,
                    item.Hash,
                    item.Activo,
                    item.FechaCreacion,
                    item.FechaModificacion
                );
            }

            return await EjecutarCargaMasivaAsync("CiudadanosReportadosImagenes", dataTable);
        }

        public record CiudadanosImagenesAccion(long ImagenIdExterno, long CiudadanoIdExterno, string PathCentral, string HashCentral, string RutaArchivoLocal, string Accion);
        public async Task<Resultado<IEnumerable<CiudadanosImagenesAccion>>> ObtenerDiferencias()
        {
            try
            {
                if (_dbConnection.State != ConnectionState.Open) await _dbConnection.OpenAsync();

                const string sql = @"
                    SELECT
                        COALESCE(ci.IdExterno, f.ImagenIdExterno)       AS ImagenIdExterno,
                        COALESCE(ci.CiudadanoIdExterno, c.IdExterno)    AS CiudadanoIdExterno,
                        ci.Path                                         AS PathCentral,
                        ci.Hash                                         AS HashCentral,
                        f.RutaArchivoLocal,
                        CASE
                            WHEN ci.IdExterno IS NULL THEN 'Borrar'
                            WHEN c.IdExterno IS NULL THEN 'Borrar'
                            WHEN c.Activo = 0 OR ci.Activo = 0 THEN 'Borrar'
                            WHEN f.ImagenIdExterno IS NULL THEN 'Descargar'
                            WHEN ci.Hash <> f.Hash THEN 'Descargar'
                        END AS Accion
                    FROM CiudadanosReportadosImagenes ci
                    LEFT JOIN CiudadanosReportados c
                        ON c.IdExterno = ci.CiudadanoIdExterno
                    FULL OUTER JOIN CiudadanosReportadosFotosEstado f
                        ON f.ImagenIdExterno = ci.IdExterno
                    WHERE ci.IdExterno IS NULL
                       OR c.IdExterno IS NULL
                       OR f.ImagenIdExterno IS NULL
                       OR c.Activo = 0 OR ci.Activo = 0
                       OR ci.Hash <> f.Hash;";

                var resultados = await _dbConnection.QueryAsync<CiudadanosImagenesAccion>(sql);

                return Resultado<IEnumerable<CiudadanosImagenesAccion>>.Ok(resultados);
            }
            catch (Exception ex)
            {
                return Resultado<IEnumerable<CiudadanosImagenesAccion>>.Fallo(ex);
            }
        }

        public async Task<Resultado> BorrarEstadoFotosAsync(IEnumerable<long> imagenesIdExterno)
        {
            try
            {
                if (_dbConnection.State != ConnectionState.Open) await _dbConnection.OpenAsync();

                await _dbConnection.ExecuteAsync(
                    "DELETE FROM CiudadanosReportadosFotosEstado WHERE ImagenIdExterno IN @ids",
                    new { ids = imagenesIdExterno });

                return Resultado.Ok();
            }
            catch (Exception ex)
            {
                return Resultado.Fallo(ex);
            }
        }

        public record CiudadanoFotoEstadoEntity(long ImagenIdExterno, long CiudadanoIdExterno, string Hash, string RutaArchivoLocal, DateTime FechaDescargaFotoLocal);
        public async Task<Resultado> ActualizarFotosEstadoAsync(IEnumerable<CiudadanoFotoEstadoEntity> filas)
        {
            const string sql = @"
                MERGE CiudadanosReportadosFotosEstado AS d
                USING (SELECT @ImagenIdExterno AS ImagenIdExterno, @CiudadanoIdExterno AS CiudadanoIdExterno, @Hash AS Hash, @RutaArchivoLocal AS RutaArchivoLocal, @FechaDescargaFotoLocal AS FechaDescargaFotoLocal) AS o
                    ON d.ImagenIdExterno = o.ImagenIdExterno
                WHEN MATCHED THEN UPDATE SET
                    Hash = o.Hash, RutaArchivoLocal = o.RutaArchivoLocal, FechaDescargaFotoLocal = o.FechaDescargaFotoLocal
                WHEN NOT MATCHED THEN INSERT (ImagenIdExterno, CiudadanoIdExterno, Hash, RutaArchivoLocal, FechaDescargaFotoLocal)
                    VALUES (o.ImagenIdExterno, o.CiudadanoIdExterno, o.Hash, o.RutaArchivoLocal, o.FechaDescargaFotoLocal);";

            try
            {
                if (_dbConnection.State != ConnectionState.Open) await _dbConnection.OpenAsync();

                await _dbConnection.ExecuteAsync(sql, filas);

                return Resultado.Ok();
            }
            catch (Exception ex)
            {
                return Resultado.Fallo(ex);
            }
        }

        private async Task<Resultado> EjecutarCargaMasivaAsync(string nombreTabla, DataTable datos)
        {
            if (_dbConnection.State != ConnectionState.Open) await _dbConnection.OpenAsync();

            await using var transaction = await _dbConnection.BeginTransactionAsync();

            try
            {
                var sqlConnection = (SqlConnection) _dbConnection;
                var sqlTransaction = (SqlTransaction) transaction;

                await sqlConnection.ExecuteAsync(
                    $"TRUNCATE TABLE {nombreTabla};",
                    transaction: sqlTransaction);

                using (var bulkCopy = new SqlBulkCopy(sqlConnection, SqlBulkCopyOptions.TableLock, sqlTransaction))
                {
                    bulkCopy.DestinationTableName = nombreTabla;
                    bulkCopy.BulkCopyTimeout = 120;

                    foreach (DataColumn column in datos.Columns)
                    {
                        bulkCopy.ColumnMappings.Add(column.ColumnName, column.ColumnName);
                    }

                    await bulkCopy.WriteToServerAsync(datos);
                }

                await transaction.CommitAsync();

                return Resultado.Ok();
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();

                return Resultado.Fallo($"Error en BulkCopy para la tabla {nombreTabla}: {ex.Message}");
            }
        }

    }
}
