using Microsoft.Data.SqlClient;
using System.Data;
using System.Data.Common;
using ViisionRemolques.Utils;

namespace ViisionRemolques.Repositories
{
    public class SensoresRepository
    {
        // Tamaño de lote con el que SqlBulkCopy envía las filas al servidor.
        private const int TamanoLote = 1000;

        private readonly DbConnection _dbConnection;

        public SensoresRepository(DbConnection dbConnection)
        {
            _dbConnection = dbConnection;
        }

        public async Task<Resultado<int>> InsertarMainLogsAsync(List<SensorMainLogEntity>? registros)
        {
            registros ??= new List<SensorMainLogEntity>();

            using var dataTable = new DataTable();

            // IdInterno se omite: es IDENTITY y lo asigna el servidor.
            dataTable.Columns.Add("GatewayId", typeof(Guid));
            dataTable.Columns.Add("LogId", typeof(int));
            dataTable.Columns.Add("LogType", typeof(string));
            dataTable.Columns.Add("DeviceId", typeof(int));
            dataTable.Columns.Add("DeviceName", typeof(string));
            dataTable.Columns.Add("DeviceType", typeof(string));
            dataTable.Columns.Add("EventType", typeof(string));
            dataTable.Columns.Add("Description", typeof(string));
            dataTable.Columns.Add("NumericValue", typeof(decimal));
            dataTable.Columns.Add("Unit", typeof(string));
            dataTable.Columns.Add("BooleanValue", typeof(bool));
            dataTable.Columns.Add("RecordedAt", typeof(DateTime));
            dataTable.Columns.Add("Sincronizado", typeof(bool));

            foreach (var registro in registros)
            {
                dataTable.Rows.Add(
                    (object?)registro.GatewayId ?? DBNull.Value,
                    (object?)registro.LogId ?? DBNull.Value,
                    (object?)registro.LogType ?? DBNull.Value,
                    (object?)registro.DeviceId ?? DBNull.Value,
                    (object?)registro.DeviceName ?? DBNull.Value,
                    (object?)registro.DeviceType ?? DBNull.Value,
                    (object?)registro.EventType ?? DBNull.Value,
                    (object?)registro.Description ?? DBNull.Value,
                    (object?)registro.NumericValue ?? DBNull.Value,
                    (object?)registro.Unit ?? DBNull.Value,
                    (object?)registro.BooleanValue ?? DBNull.Value,
                    (object?)registro.RecordedAt ?? DBNull.Value,
                    false
                );
            }

            return await EjecutarCargaMasivaAsync("dbo.SensoresMainLog", dataTable);
        }

        public async Task<Resultado<int>> InsertarAlertasAsync(List<SensorLogAlertEntity>? alertas)
        {
            alertas ??= new List<SensorLogAlertEntity>();

            using var dataTable = new DataTable();

            dataTable.Columns.Add("GatewayId", typeof(Guid));
            dataTable.Columns.Add("AlertId", typeof(int));
            dataTable.Columns.Add("AlertName", typeof(string));
            dataTable.Columns.Add("LogType", typeof(string));
            dataTable.Columns.Add("LogId", typeof(int));
            dataTable.Columns.Add("DeviceId", typeof(int));
            dataTable.Columns.Add("ConfigAlertId", typeof(int));
            dataTable.Columns.Add("AlertType", typeof(string));
            dataTable.Columns.Add("ConfigName", typeof(string));
            dataTable.Columns.Add("ConfigDescription", typeof(string));
            dataTable.Columns.Add("ThresholdValue", typeof(decimal));
            dataTable.Columns.Add("MeasuredValue", typeof(decimal));
            dataTable.Columns.Add("Unit", typeof(string));
            dataTable.Columns.Add("CreatedAt", typeof(DateTime));
            dataTable.Columns.Add("Sincronizado", typeof(bool));

            foreach (var alerta in alertas)
            {
                dataTable.Rows.Add(
                    (object?)alerta.GatewayId ?? DBNull.Value,
                    (object?)alerta.AlertId ?? DBNull.Value,
                    (object?)alerta.AlertName ?? DBNull.Value,
                    (object?)alerta.LogType ?? DBNull.Value,
                    (object?)alerta.LogId ?? DBNull.Value,
                    (object?)alerta.DeviceId ?? DBNull.Value,
                    (object?)alerta.ConfigAlertId ?? DBNull.Value,
                    (object?)alerta.AlertType ?? DBNull.Value,
                    (object?)alerta.ConfigName ?? DBNull.Value,
                    (object?)alerta.ConfigDescription ?? DBNull.Value,
                    (object?)alerta.ThresholdValue ?? DBNull.Value,
                    (object?)alerta.MeasuredValue ?? DBNull.Value,
                    (object?)alerta.Unit ?? DBNull.Value,
                    (object?)alerta.CreatedAt ?? DBNull.Value,
                    false
                );
            }

            return await EjecutarCargaMasivaAsync("dbo.SensoresLogAlert", dataTable);
        }

        private async Task<Resultado<int>> EjecutarCargaMasivaAsync(string nombreTabla, DataTable datos)
        {
            if (datos.Rows.Count == 0) return Resultado<int>.Ok(0);

            if (_dbConnection.State != ConnectionState.Open) await _dbConnection.OpenAsync();

            await using var transaction = await _dbConnection.BeginTransactionAsync();

            try
            {
                var sqlConnection = (SqlConnection) _dbConnection;
                var sqlTransaction = (SqlTransaction) transaction;

                // A diferencia de la carga de catálogos, aquí no se trunca: son bitácoras que se acumulan.
                using (var bulkCopy = new SqlBulkCopy(sqlConnection, SqlBulkCopyOptions.Default, sqlTransaction))
                {
                    bulkCopy.DestinationTableName = nombreTabla;
                    bulkCopy.BulkCopyTimeout = 120;
                    bulkCopy.BatchSize = TamanoLote;

                    foreach (DataColumn column in datos.Columns)
                    {
                        bulkCopy.ColumnMappings.Add(column.ColumnName, column.ColumnName);
                    }

                    await bulkCopy.WriteToServerAsync(datos);
                }

                await transaction.CommitAsync();

                // La carga es atómica: si no hubo excepción, se insertaron todas las filas.
                return Resultado<int>.Ok(datos.Rows.Count);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();

                return Resultado<int>.Fallo($"Error en BulkCopy para la tabla {nombreTabla}: {ex.Message}", ex);
            }
        }
    }

    public class SensorMainLogEntity
    {
        public long IdInterno { get; set; }
        public Guid? GatewayId { get; set; }
        public int? LogId { get; set; }
        public string? LogType { get; set; }
        public int? DeviceId { get; set; }
        public string? DeviceName { get; set; }
        public string? DeviceType { get; set; }
        public string? EventType { get; set; }
        public string? Description { get; set; }
        public decimal? NumericValue { get; set; }
        public string? Unit { get; set; }
        public bool? BooleanValue { get; set; }
        public DateTime? RecordedAt { get; set; }
        public bool Sincronizado { get; set; }
    }

    public class SensorLogAlertEntity
    {
        public long IdInterno { get; set; }
        public Guid? GatewayId { get; set; }
        public int? AlertId { get; set; }
        public string? AlertName { get; set; }
        public string? LogType { get; set; }
        public int? LogId { get; set; }
        public int? DeviceId { get; set; }
        public int? ConfigAlertId { get; set; }
        public string? AlertType { get; set; }
        public string? ConfigName { get; set; }
        public string? ConfigDescription { get; set; }
        public decimal? ThresholdValue { get; set; }
        public decimal? MeasuredValue { get; set; }
        public string? Unit { get; set; }
        public DateTime? CreatedAt { get; set; }
        public bool Sincronizado { get; set; }
    }
}
