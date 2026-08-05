namespace Ats.UnitTests;

using System.Threading;
using System.Threading.Tasks;
using Ats.Db;
using Ats.Service;
using Xunit;

public class SystemStatusServiceTests
{
    private sealed class FakeDatabaseHealthCheck : IDatabaseHealthCheck
    {
        private readonly DatabaseHealth _health;

        public FakeDatabaseHealthCheck(bool reachable, bool schemaCurrent)
        {
            _health = new DatabaseHealth(reachable, schemaCurrent);
        }

        public Task<DatabaseHealth> CheckAsync(CancellationToken ct = default)
        {
            return Task.FromResult(_health);
        }
    }

    private sealed class FakeVersionProvider : IVersionProvider
    {
        private readonly string _version;

        public FakeVersionProvider(string version = "1.0.0")
        {
            _version = version;
        }

        public string GetVersion() => _version;
    }

    [Fact]
    public async Task SystemStatusService_WhenDatabaseHealthy_ReturnsHealthyResult()
    {
        var fakeHealth = new FakeDatabaseHealthCheck(reachable: true, schemaCurrent: true);
        var fakeVersion = new FakeVersionProvider("1.0.0");
        var service = new SystemStatusService(fakeHealth, fakeVersion);

        var result = await service.GetStatusAsync();

        Assert.NotNull(result);
        Assert.Equal("1.0.0", result.Version);
        Assert.True(result.DatabaseReachable);
        Assert.True(result.DatabaseSchemaCurrent);
    }

    [Fact]
    public async Task SystemStatusService_WhenDatabaseUnreachable_ReturnsUnhealthyResult()
    {
        var fakeHealth = new FakeDatabaseHealthCheck(reachable: false, schemaCurrent: false);
        var fakeVersion = new FakeVersionProvider("1.0.0");
        var service = new SystemStatusService(fakeHealth, fakeVersion);

        var result = await service.GetStatusAsync();

        Assert.NotNull(result);
        Assert.Equal("1.0.0", result.Version);
        Assert.False(result.DatabaseReachable);
        Assert.False(result.DatabaseSchemaCurrent);
    }
}
