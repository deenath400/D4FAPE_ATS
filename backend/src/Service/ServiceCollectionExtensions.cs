namespace Ats.Service;

using Ats.Db;
using Ats.Service.Application;
using Ats.Service.Auth;
using Ats.Service.Pipeline;
using Ats.Service.Requisition;
using Ats.Shared.Auth;
using Ats.Shared.Storage;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSystemService(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbCore(configuration);

        services.AddIdentityCore<ApplicationUser>(options =>
        {
            options.Password.RequireDigit = true;
            options.Password.RequiredLength = 8;
            options.Password.RequireNonAlphanumeric = true;
            options.Password.RequireUppercase = true;
            options.Password.RequireLowercase = true;
            options.User.RequireUniqueEmail = true;
        })
        .AddRoles<ApplicationRole>()
        .AddEntityFrameworkStores<AppDbContext>()
        .AddSignInManager();

        services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<ISystemStatusService, SystemStatusService>();
        services.AddScoped<IRequisitionService, RequisitionService>();
        services.AddSingleton<IFileStorage, LocalDiskFileStorage>();
        services.AddScoped<IApplicationService, ApplicationService>();
        services.AddScoped<IPipelineService, PipelineService>();

        services.Configure<Ats.Service.Screening.GeminiOptions>(configuration.GetSection(Ats.Service.Screening.GeminiOptions.SectionName));

        var screeningProvider = configuration["Screening:Provider"] ?? "Mock";
        if (string.Equals(screeningProvider, "Gemini", StringComparison.OrdinalIgnoreCase))
        {
            var apiKey = configuration["Gemini:ApiKey"];
            if (!string.IsNullOrWhiteSpace(apiKey))
            {
                var timeoutSeconds = configuration.GetValue("Gemini:TimeoutSeconds", 30);
                services.AddHttpClient<Ats.Service.Screening.IScreeningService, Ats.Service.Screening.GeminiScreeningService>(client =>
                {
                    client.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
                });
            }
            else
            {
                services.AddScoped<Ats.Service.Screening.IScreeningService, Ats.Service.Screening.MockScreeningService>();
            }
        }
        else
        {
            services.AddScoped<Ats.Service.Screening.IScreeningService, Ats.Service.Screening.MockScreeningService>();
        }

        services.AddSingleton<Ats.Service.Screening.IPdfTextExtractor, Ats.Service.Screening.PdfTextExtractor>();
        services.AddScoped<Ats.Service.Screening.IScreeningOrchestrator, Ats.Service.Screening.ScreeningOrchestrator>();

        return services;
    }
}
