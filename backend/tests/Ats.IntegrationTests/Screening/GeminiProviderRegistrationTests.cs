namespace Ats.IntegrationTests.Screening;

using System.Collections.Generic;
using Ats.Service;
using Ats.Service.Screening;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

public sealed class GeminiProviderRegistrationTests
{
    [Fact]
    public void ProviderRegistration_GeminiWithKey_RegistersGeminiService()
    {
        // Arrange
        var inMemorySettings = new Dictionary<string, string?>
        {
            ["ConnectionStrings:Default"] = "Data Source=:memory:",
            ["Screening:Provider"] = "Gemini",
            ["Gemini:ApiKey"] = "fake-gemini-api-key-12345",
            ["Gemini:TimeoutSeconds"] = "10"
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSystemService(configuration);

        var provider = services.BuildServiceProvider();

        // Act
        var screeningService = provider.GetService<IScreeningService>();

        // Assert
        Assert.NotNull(screeningService);
        Assert.IsType<GeminiScreeningService>(screeningService);
    }

    [Fact]
    public void ProviderRegistration_GeminiWithoutKey_FallsBackToMockService()
    {
        // Arrange
        var inMemorySettings = new Dictionary<string, string?>
        {
            ["ConnectionStrings:Default"] = "Data Source=:memory:",
            ["Screening:Provider"] = "Gemini",
            ["Gemini:ApiKey"] = "" // Empty API key triggers graceful fallback
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSystemService(configuration);

        var provider = services.BuildServiceProvider();

        // Act
        var screeningService = provider.GetService<IScreeningService>();

        // Assert
        Assert.NotNull(screeningService);
        Assert.IsType<MockScreeningService>(screeningService);
    }

    [Fact]
    public void ProviderRegistration_MockProvider_RegistersMockService()
    {
        // Arrange
        var inMemorySettings = new Dictionary<string, string?>
        {
            ["ConnectionStrings:Default"] = "Data Source=:memory:",
            ["Screening:Provider"] = "Mock"
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSystemService(configuration);

        var provider = services.BuildServiceProvider();

        // Act
        var screeningService = provider.GetService<IScreeningService>();

        // Assert
        Assert.NotNull(screeningService);
        Assert.IsType<MockScreeningService>(screeningService);
    }

    [Fact]
    public void ProviderRegistration_DefaultUnspecified_RegistersMockService()
    {
        // Arrange
        var inMemorySettings = new Dictionary<string, string?>
        {
            ["ConnectionStrings:Default"] = "Data Source=:memory:"
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSystemService(configuration);

        var provider = services.BuildServiceProvider();

        // Act
        var screeningService = provider.GetService<IScreeningService>();

        // Assert
        Assert.NotNull(screeningService);
        Assert.IsType<MockScreeningService>(screeningService);
    }
}
