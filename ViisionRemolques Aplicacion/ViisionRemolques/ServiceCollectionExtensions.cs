using Dapper;
using Microsoft.Data.SqlClient;
using System.Data;
using ViisionRemolques.Repositories;
using ViisionRemolques.Services;
using ViisionRemolques.Services.ISAPI;

namespace ViisionRemolques
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddDapperRepositories(this IServiceCollection services, IConfiguration configuration)
        {
            DefaultTypeMap.MatchNamesWithUnderscores = true;

            services.AddTransient<IDbConnection>(sp => new SqlConnection(configuration.GetConnectionString("DatabaseConnection")));

            //Repositorios
            services.AddScoped<CamaraRepository>();
            services.AddScoped<EventoPerimetralRepository>();
            services.AddScoped<AlarmaDesconocidaLogRepository>();

            return services;
        }

        public static IServiceCollection AddApplicationServices(this IServiceCollection services)
        {
            services.AddScoped<ISAPIClientFactoryService>();
            services.AddTransient<VCAService>();

            return services;
        }
    }
}
