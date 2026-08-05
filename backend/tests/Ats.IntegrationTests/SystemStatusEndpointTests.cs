namespace Ats.IntegrationTests;

using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Ats.Api;
using Xunit;

public class SystemStatusEndpointTests
{
    [Fact]
    public async Task GetSystemStatus_WhenDatabaseMigrated_Returns200()
    {
        using var factory = new CustomWebApplicationFactory(applyMigrations: true);
        factory.InitializeDatabase();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/system/status");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var dto = await response.Content.ReadFromJsonAsync<SystemStatusDto>();
        Assert.NotNull(dto);
        Assert.False(string.IsNullOrWhiteSpace(dto.Version));
        Assert.NotNull(dto.Database);
        Assert.True(dto.Database.Reachable);
        Assert.True(dto.Database.SchemaCurrent);
    }

    [Fact]
    public async Task GetSystemStatus_WhenDatabaseFileMissing_Returns503WithoutLeakingPath()
    {
        using var factory = new CustomWebApplicationFactory(applyMigrations: false);
        // Note: Do not run factory.InitializeDatabase(), database file is missing/unmigrated
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/system/status");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();

        // Ensure database file path or connection string is not leaked in response body (AC-11)
        Assert.DoesNotContain(factory.DbFilePath, content);
        Assert.DoesNotContain("Data Source", content);
        Assert.Contains("system.status.database-unavailable", content);
    }

    [Fact]
    public async Task GetSystemStatus_NeverReceivesAuthorizationHeader()
    {
        using var factory = new CustomWebApplicationFactory(applyMigrations: true);
        factory.InitializeDatabase();
        using var client = factory.CreateClient();

        // Ensure client sends no Authorization header
        Assert.Null(client.DefaultRequestHeaders.Authorization);

        var response = await client.GetAsync("/api/system/status");

        // System status is anonymous (AC-27)
        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
