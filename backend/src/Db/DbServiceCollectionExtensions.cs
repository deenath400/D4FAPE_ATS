namespace Ats.Db;

using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

public static class DbServiceCollectionExtensions
{
    public static IServiceCollection AddDbCore(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AppDbContext>((sp, options) =>
        {
            var config = sp.GetRequiredService<IConfiguration>();
            var connectionString = config.GetConnectionString("Default");
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException("Missing required configuration key 'ConnectionStrings:Default'.");
            }

            options.UseSqlite(connectionString)
                   .AddInterceptors(new SqlitePragmaConnectionInterceptor());
        });

        services.AddScoped<IDatabaseHealthCheck, EfDatabaseHealthCheck>();
        return services;
    }
}
