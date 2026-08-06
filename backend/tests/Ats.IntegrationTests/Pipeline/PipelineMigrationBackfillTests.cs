namespace Ats.IntegrationTests.Pipeline;

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Ats.Db;
using Ats.Db.Requisitions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

/// <summary>
/// AC-32 / R-2 (HLD §9): the <c>AddPipelineProgression</c> migration's raw-SQL backfill steps
/// (erd.md §5, steps 8-9) must seed the default 4-Stage set for every pre-existing Requisition
/// with zero Stages, assign every pre-existing Application to its Requisition's first Stage, and
/// write no <c>StageTransition</c> row for the backfill itself (FR-25). Each test migrates a
/// fresh SQLite file only as far as the migration immediately preceding
/// <c>AddPipelineProgression</c>, inserts rows via raw ADO.NET (the current entity shapes cannot
/// express the pre-0005 schema), then migrates to latest and asserts the result via
/// <see cref="AppDbContext"/>.
/// </summary>
public class PipelineMigrationBackfillTests : IDisposable
{
    // Full migration id (timestamp-prefixed) — IMigrator.MigrateAsync requires the exact id,
    // not the bare class name.
    private const string PreviousMigration = "20260805191845_AddApplicationsAndCvAttachments";

    private readonly string _dbFilePath;

    public PipelineMigrationBackfillTests()
    {
        _dbFilePath = Path.Combine(Path.GetTempPath(), $"ats_pipeline_migration_{Guid.NewGuid():N}.db");
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

    private AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={_dbFilePath}")
            .Options;
        return new AppDbContext(options);
    }

    private async Task MigrateToPreviousAsync()
    {
        // DatabaseFacade.MigrateAsync() has no targetMigration overload — only IMigrator does
        // (the mechanism `dotnet ef database update <name>` uses internally).
        await using var context = CreateContext();
        var migrator = context.GetInfrastructure().GetRequiredService<IMigrator>();
        await migrator.MigrateAsync(PreviousMigration);
    }

    private async Task MigrateToLatestAsync()
    {
        await using var context = CreateContext();
        await context.Database.MigrateAsync();
    }

    private async Task<SqliteConnection> OpenRawConnectionAsync()
    {
        var connection = new SqliteConnection($"Data Source={_dbFilePath}");
        await connection.OpenAsync();
        return connection;
    }

    // Guid parameters are bound as raw `Guid` values (never `.ToString()`) so the on-disk TEXT
    // representation matches exactly what EF Core's own Sqlite Guid marshalling produces —
    // otherwise a row inserted here would never satisfy an EF LINQ equality comparison in the
    // verification step below, despite both being "text" storage class (see implementation/
    // changelog.md CP-1 Deviations for how this was diagnosed).
    private static async Task InsertRequisitionAsync(SqliteConnection connection, Guid id, string status = "Published")
    {
        var cmd = connection.CreateCommand();
        cmd.CommandText = @"
INSERT INTO Requisitions (Id, Title, Description, Status, CreatedAtUtc, UpdatedAtUtc)
VALUES ($id, 'Pre-existing Role', 'Description', $status, datetime('now'), datetime('now'));";
        cmd.Parameters.AddWithValue("$id", id);
        cmd.Parameters.AddWithValue("$status", status);
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task InsertUserAsync(SqliteConnection connection, Guid id, string email)
    {
        var cmd = connection.CreateCommand();
        cmd.CommandText = @"
INSERT INTO AspNetUsers
    (Id, FirstName, LastName, CreatedAtUtc, UserName, NormalizedUserName, Email, NormalizedEmail,
     EmailConfirmed, PasswordHash, SecurityStamp, ConcurrencyStamp, PhoneNumberConfirmed,
     TwoFactorEnabled, LockoutEnabled, AccessFailedCount)
VALUES ($id, 'Ada', 'Lovelace', datetime('now'), $email, $normalizedEmail, $email, $normalizedEmail,
        1, 'x', 'x', 'x', 0, 0, 1, 0);";
        cmd.Parameters.AddWithValue("$id", id);
        cmd.Parameters.AddWithValue("$email", email);
        cmd.Parameters.AddWithValue("$normalizedEmail", email.ToUpperInvariant());
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task InsertApplicationAsync(SqliteConnection connection, Guid id, Guid requisitionId, Guid candidateId)
    {
        var cmd = connection.CreateCommand();
        cmd.CommandText = @"
INSERT INTO Applications (Id, RequisitionId, CandidateId, SubmittedAtUtc)
VALUES ($id, $requisitionId, $candidateId, datetime('now'));";
        cmd.Parameters.AddWithValue("$id", id);
        cmd.Parameters.AddWithValue("$requisitionId", requisitionId);
        cmd.Parameters.AddWithValue("$candidateId", candidateId);
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task InsertStageAsync(SqliteConnection connection, Guid id, Guid requisitionId, string name)
    {
        var cmd = connection.CreateCommand();
        cmd.CommandText = "INSERT INTO Stages (Id, RequisitionId, Name) VALUES ($id, $requisitionId, $name);";
        cmd.Parameters.AddWithValue("$id", id);
        cmd.Parameters.AddWithValue("$requisitionId", requisitionId);
        cmd.Parameters.AddWithValue("$name", name);
        await cmd.ExecuteNonQueryAsync();
    }

    [Fact]
    public async Task Migration_Backfill_SeedsDefaultStagesForExistingRequisitions()
    {
        await MigrateToPreviousAsync();

        var requisitionId = Guid.NewGuid();
        await using (var connection = await OpenRawConnectionAsync())
        {
            await InsertRequisitionAsync(connection, requisitionId);
        }

        await MigrateToLatestAsync();

        await using var verifyContext = CreateContext();
        var stages = await verifyContext.Stages
            .AsNoTracking()
            .Where(s => s.RequisitionId == requisitionId)
            .OrderBy(s => s.SortOrder)
            .ToListAsync();

        Assert.Equal(4, stages.Count);
        Assert.Equal(Stage.DefaultStageNames, stages.Select(s => s.Name).ToArray());
        Assert.Equal(new[] { 0, 1, 2, 3 }, stages.Select(s => s.SortOrder).ToArray());
        Assert.All(stages, s => Assert.Equal(s.Name.ToUpperInvariant(), s.NormalizedName));
    }

    [Fact]
    public async Task Migration_Backfill_AssignsExistingApplicationsFirstStageNoTransitionRow()
    {
        await MigrateToPreviousAsync();

        var requisitionId = Guid.NewGuid();
        var candidateId = Guid.NewGuid();
        var applicationId = Guid.NewGuid();

        await using (var connection = await OpenRawConnectionAsync())
        {
            await InsertRequisitionAsync(connection, requisitionId);
            await InsertUserAsync(connection, candidateId, "candidate@example.com");
            await InsertApplicationAsync(connection, applicationId, requisitionId, candidateId);
        }

        await MigrateToLatestAsync();

        await using var verifyContext = CreateContext();
        var application = await verifyContext.Applications.AsNoTracking().SingleAsync(a => a.Id == applicationId);
        var firstStage = await verifyContext.Stages
            .AsNoTracking()
            .Where(s => s.RequisitionId == requisitionId)
            .OrderBy(s => s.SortOrder)
            .FirstAsync();

        Assert.Equal(firstStage.Id, application.CurrentStageId);
        Assert.False(application.IsRejected);

        var transitions = await verifyContext.StageTransitions
            .AsNoTracking()
            .Where(t => t.ApplicationId == applicationId)
            .ToListAsync();
        Assert.Empty(transitions);
    }

    [Fact]
    public async Task Migration_Backfill_SeededNamesMatchDefaultStageNamesConstant()
    {
        // R-2 (HLD §9): the migration's raw SQL cannot reference Stage.DefaultStageNames
        // directly — this test is the drift detector.
        await MigrateToPreviousAsync();

        var requisitionId = Guid.NewGuid();
        await using (var connection = await OpenRawConnectionAsync())
        {
            await InsertRequisitionAsync(connection, requisitionId, status: "Draft");
        }

        await MigrateToLatestAsync();

        await using var verifyContext = CreateContext();
        var seededNames = await verifyContext.Stages
            .AsNoTracking()
            .Where(s => s.RequisitionId == requisitionId)
            .OrderBy(s => s.SortOrder)
            .Select(s => s.Name)
            .ToListAsync();

        Assert.Equal(Stage.DefaultStageNames, seededNames);
    }

    [Fact]
    public async Task Migration_Backfill_RequisitionWithExistingStage_NotDuplicated()
    {
        // A Requisition that already has at least one Stage (should not occur pre-0005 in
        // practice, since 0003 shipped no Stage-writing code path, but the backfill's
        // NOT EXISTS guard must not double-seed one that somehow does) is left untouched.
        await MigrateToPreviousAsync();

        var requisitionId = Guid.NewGuid();
        var existingStageId = Guid.NewGuid();

        await using (var connection = await OpenRawConnectionAsync())
        {
            await InsertRequisitionAsync(connection, requisitionId, status: "Draft");
            await InsertStageAsync(connection, existingStageId, requisitionId, "Custom");
        }

        await MigrateToLatestAsync();

        await using var verifyContext = CreateContext();
        var stages = await verifyContext.Stages.AsNoTracking().Where(s => s.RequisitionId == requisitionId).ToListAsync();

        Assert.Single(stages);
        Assert.Equal(existingStageId, stages[0].Id);
        Assert.Equal("Custom", stages[0].Name);
    }

    [Fact]
    public async Task Migration_ThenApply_AllMigrationsRunCleanlyFromScratch()
    {
        // AC-32's own precondition: the migration set (including AddPipelineProgression) applies
        // cleanly end-to-end on a brand-new database with no pre-existing rows at all.
        await MigrateToLatestAsync();

        await using var verifyContext = CreateContext();
        var appliedMigrations = await verifyContext.Database.GetAppliedMigrationsAsync();

        Assert.Contains(appliedMigrations, m => m.EndsWith("_AddPipelineProgression", StringComparison.Ordinal));
        Assert.Empty(await verifyContext.Stages.AsNoTracking().ToListAsync());
        Assert.Empty(await verifyContext.Applications.AsNoTracking().ToListAsync());
    }
}
