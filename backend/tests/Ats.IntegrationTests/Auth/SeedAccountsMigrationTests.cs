namespace Ats.IntegrationTests.Auth;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Ats.Db;
using Ats.Service;
using Ats.Shared.Auth;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Xunit;

/// <summary>
/// AC-1, AC-6, AC-7, AC-8, NFR-1, E-4 (spec 0007): the <c>AddSeedSampleAccounts</c> migration's
/// <c>HasData</c> rows (<see cref="AppDbContext"/>'s private <c>SeedUsers</c>, documented in
/// plan/erd.md §7) must seed exactly the 3 sample accounts and their 3 role assignments on every
/// fresh database, independent of host environment, and must not duplicate rows across repeated
/// migration runs — EF Core's own migration-history bookkeeping is what should make the last part
/// true; these tests are the drift detector that would catch a regression.
/// </summary>
public class SeedAccountsMigrationTests : IDisposable
{
    private readonly string _dbFilePath;

    public SeedAccountsMigrationTests()
    {
        _dbFilePath = Path.Combine(Path.GetTempPath(), $"ats_seed_accounts_{Guid.NewGuid():N}.db");
    }

    public void Dispose()
    {
        try
        {
            if (File.Exists(_dbFilePath)) File.Delete(_dbFilePath);
            var wal = _dbFilePath + "-wal";
            if (File.Exists(wal)) File.Delete(wal);
            var shm = _dbFilePath + "-shm";
            if (File.Exists(shm)) File.Delete(shm);
        }
        catch
        {
            // Best effort cleanup.
        }

        GC.SuppressFinalize(this);
    }

    private static AppDbContext CreateContext(string dbFilePath)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={dbFilePath}")
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task Migrate_FreshDatabase_SeedsExactlyThreeUsersAndThreeRoleAssignments()
    {
        await using var context = CreateContext(_dbFilePath);
        await context.Database.MigrateAsync();

        var userCount = await context.Users.AsNoTracking().CountAsync();
        var roleAssignmentCount = await context.UserRoles.AsNoTracking().CountAsync();

        Assert.Equal(3, userCount);
        Assert.Equal(3, roleAssignmentCount);

        var seededEmails = await context.Users.AsNoTracking().Select(u => u.Email).ToListAsync();
        Assert.Contains(AuthConstants.SeedAccounts.CandidateEmail, seededEmails);
        Assert.Contains(AuthConstants.SeedAccounts.RecruiterEmail, seededEmails);
        Assert.Contains(AuthConstants.SeedAccounts.HiringManagerEmail, seededEmails);
    }

    [Fact]
    public async Task Migrate_RecruiterAccount_HasExactlyOneRoleAssignment()
    {
        await using var context = CreateContext(_dbFilePath);
        await context.Database.MigrateAsync();

        var recruiterRoleAssignments = await context.UserRoles
            .AsNoTracking()
            .Where(r => r.UserId == AuthConstants.SeedAccounts.RecruiterUserId)
            .ToListAsync();

        Assert.Single(recruiterRoleAssignments);
        Assert.Equal(AuthConstants.Roles.RecruiterRoleId, recruiterRoleAssignments[0].RoleId);
    }

    [Fact]
    public async Task Migrate_TwiceInARow_DoesNotDuplicateSeededRows()
    {
        await using (var firstContext = CreateContext(_dbFilePath))
        {
            await firstContext.Database.MigrateAsync();
        }

        await using (var secondContext = CreateContext(_dbFilePath))
        {
            // No pending migrations remain at this point — this exercises the same no-op path a
            // redeploy against an already-migrated database takes (AC-8, E-4).
            await secondContext.Database.MigrateAsync();
        }

        await using var verifyContext = CreateContext(_dbFilePath);
        Assert.Equal(3, await verifyContext.Users.AsNoTracking().CountAsync());
        Assert.Equal(3, await verifyContext.UserRoles.AsNoTracking().CountAsync());
    }

    [Fact]
    public async Task Migrate_WithProductionEnvironment_StillSeedsAllThreeAccounts()
    {
        // Exercises the same Ats.Service.ServiceCollectionExtensions.AddSystemService DI wiring
        // that Program.cs uses (AppDbContext resolved through the real AddDbCore/AddIdentityCore
        // registration), with a `IHostEnvironment`/`IWebHostEnvironment` explicitly set to
        // "Production" registered in the container (AC-7) — proves seeding is baked into the
        // migration itself at build time and has no dependency on host environment at run time.
        //
        // Deviation from plan/lld.md §11 ("factory built with UseEnvironment(\"Production\")"):
        // going through a full ASP.NET Core `WebApplicationFactory<Program>` with `UseEnvironment`
        // set to a non-default value causes the minimal-hosting test bootstrap to re-invoke
        // Program.cs's top-level statements via a codepath where the test's injected
        // `ConnectionStrings:Default` override is not visible yet to Program.cs's own early
        // `Environment.Exit(1)` connection-string guard, crashing the whole test host process
        // before a single assertion runs. Building the same `AddSystemService` container directly
        // sidesteps that guard while still exercising the real seeding path. See
        // implementation/changelog.md CP-1 Deviations.
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = $"Data Source={_dbFilePath}"
            })
            .Build();

        var environment = new ProductionHostEnvironment();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton<IHostEnvironment>(environment);
        services.AddSingleton<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>(environment);
        services.AddSystemService(configuration);

        await using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();

        var seededEmails = await db.Users.AsNoTracking().Select(u => u.Email).ToListAsync();

        Assert.Equal(3, seededEmails.Count);
        Assert.Contains(AuthConstants.SeedAccounts.CandidateEmail, seededEmails);
        Assert.Contains(AuthConstants.SeedAccounts.RecruiterEmail, seededEmails);
        Assert.Contains(AuthConstants.SeedAccounts.HiringManagerEmail, seededEmails);
    }

    /// <summary>
    /// Minimal <c>IWebHostEnvironment</c> (which also satisfies <c>IHostEnvironment</c>) fixed to
    /// "Production" — nothing in the current seeding path reads it, but registering a real
    /// instance in the container is what makes this test a genuine drift detector if a future
    /// change ever wires environment checks into <see cref="AppDbContext"/> or its seeding.
    /// </summary>
    private sealed class ProductionHostEnvironment : Microsoft.AspNetCore.Hosting.IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = Microsoft.Extensions.Hosting.Environments.Production;
        public string ApplicationName { get; set; } = "Ats.Api";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }
}
