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
                dataTable.Rows.Add(
                    item.IdExterno,
                    (object?)item.Nombre ?? DBNull.Value,
                    (object?)item.ApellidoPaterno ?? DBNull.Value,
                    (object?)item.ApellidoMaterno ?? DBNull.Value,
                    (object?)item.Sexo ?? DBNull.Value,
                    item.FechaNacimiento,
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

        public async Task<Resultado> ActualizarImagenes(List<CiudadanoReportadoImagen> ciudadanosReportadosImagenes)
        {
            ciudadanosReportadosImagenes ??= new List<CiudadanoReportadoImagen>();

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
                dataTable.Rows.Add(
                    item.IdExterno,
                    item.CiudadanoIdExterno,
                    (object?)item.Path ?? DBNull.Value,
                    (object?)item.Hash ?? DBNull.Value,
                    item.Activo,
                    item.FechaCreacion,
                    item.FechaModificacion
                );
            }

            return await EjecutarCargaMasivaAsync("CiudadanosReportadosImagenes", dataTable);
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
