using Dapper;
using System.Data;
using System.Data.Common;
using ViisionRemolques.Enums;
using ViisionRemolques.Utils;

namespace ViisionRemolques.Repositories
{
    public class CatalogoVersionEntity
    {
        public int Catalogo { get; set; }
        public string? Version { get; set; }
    }

    public class CatalogosVersionesRepository
    {
        private readonly DbConnection _dbConnection;

        public CatalogosVersionesRepository(DbConnection dbConnection)
        {
            _dbConnection = dbConnection;
        }

        public async Task<Resultado<CatalogoVersionEntity>> ObtenerAsync(CatalogoVersionEnum catalogo)
        {
            const string sql = @"
                MERGE CatalogosVersiones AS destino
                USING (SELECT @Catalogo AS Catalogo) AS origen
                ON destino.Catalogo = origen.Catalogo
                WHEN NOT MATCHED THEN
                    INSERT (Catalogo, Version) VALUES (origen.Catalogo, NULL);

                SELECT Catalogo, Version FROM CatalogosVersiones WHERE Catalogo = @Catalogo;";

            try
            {
                if (_dbConnection.State != ConnectionState.Open) await _dbConnection.OpenAsync();

                var entidad = await _dbConnection.QueryFirstAsync<CatalogoVersionEntity>(sql, new { Catalogo = (int)catalogo });

                return Resultado<CatalogoVersionEntity>.Ok(entidad);
            }
            catch (Exception ex)
            {
                return Resultado<CatalogoVersionEntity>.Fallo(ex);
            }
        }

        public async Task<Resultado> ActualizarVersionAsync(CatalogoVersionEnum catalogo, string? version)
        {
            const string sql = @"
                MERGE CatalogosVersiones AS destino
                USING (SELECT @Catalogo AS Catalogo, @Version AS Version) AS origen
                ON destino.Catalogo = origen.Catalogo
                WHEN MATCHED THEN UPDATE SET Version = origen.Version
                WHEN NOT MATCHED THEN INSERT (Catalogo, Version) VALUES (origen.Catalogo, origen.Version);";

            try
            {
                if (_dbConnection.State != ConnectionState.Open) await _dbConnection.OpenAsync();

                await _dbConnection.ExecuteAsync(sql, new { Catalogo = (int)catalogo, Version = version });

                return Resultado.Ok();
            }
            catch (Exception ex)
            {
                return Resultado.Fallo(ex);
            }
        }
    }
}
