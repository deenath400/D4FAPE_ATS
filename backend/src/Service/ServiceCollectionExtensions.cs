namespace Ats.Service;

using Ats.Db;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSystemService(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbCore(configuration);
        services.AddScoped<ISystemStatusService, SystemStatusService>();
        return services;
    }
}
