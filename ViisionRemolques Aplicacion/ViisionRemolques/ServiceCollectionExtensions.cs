using Dapper;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Data.Common;
using ViisionRemolques.Jobs;
using ViisionRemolques.Repositories;
using ViisionRemolques.Repositories.Eventos;
using ViisionRemolques.Services;
using ViisionRemolques.Services.ISAPI;

namespace ViisionRemolques
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddDapperRepositories(this IServiceCollection services, IConfiguration configuration)
        {
            DefaultTypeMap.MatchNamesWithUnderscores = true;

            services.AddTransient<DbConnection>(sp => new SqlConnection(configuration.GetConnectionString("DatabaseConnection")));

            //Repositorios
            services.AddScoped<CamaraRepository>();

            services.AddScoped<ImagenesRepository>();
            services.AddScoped<EventoSmartRepository>();
            services.AddScoped<EventoAlarmaRecuentoPersonasRepository>();
            services.AddScoped<EventoANPRRepository>();
            services.AddScoped<EventoArmadoPistaPersonaRepository>();
            services.AddScoped<EventoCapturaFacialRepository>();
            services.AddScoped<EventoDeteccionTipoMultiobjetivoRepository>();
            services.AddScoped<EventoRecuentoPersonasRepository>();
            services.AddScoped<EventoTraficoRodadoRepository>();

            return services;
        }

        public static IServiceCollection AddApplicationServices(this IServiceCollection services)
        {
            services.AddScoped<ISAPIClientFactoryService>();
            services.AddTransient<VCAService>();
            services.AddTransient<HeatmapService>();
            services.AddTransient<InterseccionesService>();

            services.AddTransient<AlmacenamientoImagenesService>();
            services.AddTransient<WebhookPayloadExtractorService>();

            return services;
        }


        public static IServiceCollection AddApplicationJobs(this IServiceCollection services)
        {
            services.AddTransient<ProcesadorEventosWebhookJob>();

            return services;
        }
    }
}
