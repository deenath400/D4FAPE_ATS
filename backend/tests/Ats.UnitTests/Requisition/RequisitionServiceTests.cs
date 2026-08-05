namespace Ats.UnitTests.Requisition;

using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ats.Db;
using Ats.Service.Common;
using Ats.Service.Requisition;
using Ats.Service.Requisition.Dtos;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Xunit;

public class RequisitionServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _dbContext;
    private readonly RequisitionService _service;

    public RequisitionServiceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        _dbContext = new AppDbContext(options);
        _dbContext.Database.EnsureCreated();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Requisitions:DefaultPageSize"] = "20",
                ["Requisitions:MaxPageSize"] = "50"
            })
            .Build();

        _service = new RequisitionService(_dbContext, configuration);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }

    private async Task<RequisitionDto> CreateAsync(string title = "Senior Engineer", string description = "Build things.")
    {
        var result = await _service.CreateAsync(new CreateRequisitionRequestDto(title, description));
        Assert.True(result.IsSuccess);
        return result.Value!;
    }

    private async Task<RequisitionDto> CreateAndPublishAsync(string title = "Senior Engineer", string description = "Build things.")
    {
        var created = await CreateAsync(title, description);
        var published = await _service.PublishAsync(created.Id);
        Assert.True(published.IsSuccess);
        return published.Value!;
    }

    [Fact]
    public async Task CreateAsync_WithValidPayload_ReturnsDraft()
    {
        var result = await _service.CreateAsync(new CreateRequisitionRequestDto("Senior Backend Engineer", "We are looking for..."));

        Assert.True(result.IsSuccess);
        Assert.Equal("draft", result.Value!.Status);
        Assert.Equal("Senior Backend Engineer", result.Value.Title);
    }

    [Fact]
    public async Task CreateAsync_WithEmptyTitle_ReturnsValidationError()
    {
        var result = await _service.CreateAsync(new CreateRequisitionRequestDto("", "Description"));

        Assert.False(result.IsSuccess);
        Assert.Equal(ResultStatus.Validation, result.Status);
        Assert.Equal("requisition.create.validation-failed", result.ErrorCode);
    }

    [Fact]
    public async Task UpdateContentAsync_WhileDraft_UpdatesFields()
    {
        var created = await CreateAsync();

        var result = await _service.UpdateContentAsync(created.Id, new UpdateRequisitionRequestDto("New Title", "New Description"));

        Assert.True(result.IsSuccess);
        Assert.Equal("New Title", result.Value!.Title);
        Assert.Equal("New Description", result.Value.Description);
        Assert.Equal("draft", result.Value.Status);
    }

    [Fact]
    public async Task UpdateContentAsync_WhilePublished_UpdatesFieldsKeepsStatus()
    {
        var published = await CreateAndPublishAsync();

        var result = await _service.UpdateContentAsync(published.Id, new UpdateRequisitionRequestDto("Updated Title", "Updated Description"));

        Assert.True(result.IsSuccess);
        Assert.Equal("Updated Title", result.Value!.Title);
        Assert.Equal("published", result.Value.Status);
    }

    [Fact]
    public async Task UpdateContentAsync_WhileClosed_ReturnsConflict()
    {
        var published = await CreateAndPublishAsync();
        var closed = await _service.CloseAsync(published.Id);
        Assert.True(closed.IsSuccess);

        var result = await _service.UpdateContentAsync(published.Id, new UpdateRequisitionRequestDto("New Title", "New Description"));

        Assert.False(result.IsSuccess);
        Assert.Equal(ResultStatus.Conflict, result.Status);
        Assert.Equal("requisition.update.closed", result.ErrorCode);
    }

    [Fact]
    public async Task PublishAsync_FromDraft_ReturnsPublished()
    {
        var created = await CreateAsync();

        var result = await _service.PublishAsync(created.Id);

        Assert.True(result.IsSuccess);
        Assert.Equal("published", result.Value!.Status);
    }

    [Fact]
    public async Task UnpublishAsync_FromPublished_ReturnsDraft()
    {
        var published = await CreateAndPublishAsync();

        var result = await _service.UnpublishAsync(published.Id);

        Assert.True(result.IsSuccess);
        Assert.Equal("draft", result.Value!.Status);
    }

    [Fact]
    public async Task PublishAsync_AfterUnpublishAndEdit_ReflectsEditedContent()
    {
        var published = await CreateAndPublishAsync();
        await _service.UnpublishAsync(published.Id);
        await _service.UpdateContentAsync(published.Id, new UpdateRequisitionRequestDto("Edited Title", "Edited Description"));

        var result = await _service.PublishAsync(published.Id);

        Assert.True(result.IsSuccess);
        Assert.Equal("published", result.Value!.Status);
        Assert.Equal("Edited Title", result.Value.Title);
        Assert.Equal("Edited Description", result.Value.Description);
    }

    [Fact]
    public async Task CloseAsync_FromPublished_ReturnsClosed()
    {
        var published = await CreateAndPublishAsync();

        var result = await _service.CloseAsync(published.Id);

        Assert.True(result.IsSuccess);
        Assert.Equal("closed", result.Value!.Status);
    }

    [Fact]
    public async Task CloseAsync_FromDraft_ReturnsConflict()
    {
        var created = await CreateAsync();

        var result = await _service.CloseAsync(created.Id);

        Assert.False(result.IsSuccess);
        Assert.Equal(ResultStatus.Conflict, result.Status);
        Assert.Equal("requisition.close.invalid-transition", result.ErrorCode);
    }

    [Fact]
    public async Task PublishAsync_FromClosed_ReturnsConflict()
    {
        var published = await CreateAndPublishAsync();
        await _service.CloseAsync(published.Id);

        var result = await _service.PublishAsync(published.Id);

        Assert.False(result.IsSuccess);
        Assert.Equal(ResultStatus.Conflict, result.Status);
        Assert.Equal("requisition.publish.invalid-transition", result.ErrorCode);
    }

    [Fact]
    public async Task UnpublishAsync_FromClosed_ReturnsConflict()
    {
        var published = await CreateAndPublishAsync();
        await _service.CloseAsync(published.Id);

        var result = await _service.UnpublishAsync(published.Id);

        Assert.False(result.IsSuccess);
        Assert.Equal(ResultStatus.Conflict, result.Status);
        Assert.Equal("requisition.unpublish.invalid-transition", result.ErrorCode);
    }

    [Fact]
    public async Task ListAsync_ReturnsAllStatuses()
    {
        var draft = await CreateAsync("Draft Role", "Description");
        var published = await CreateAndPublishAsync("Published Role", "Description");
        var closed = await CreateAndPublishAsync("Closed Role", "Description");
        await _service.CloseAsync(closed.Id);

        var result = await _service.ListAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value!.Count);
        Assert.Contains(result.Value, r => r.Id == draft.Id && r.Status == "draft");
        Assert.Contains(result.Value, r => r.Id == published.Id && r.Status == "published");
        Assert.Contains(result.Value, r => r.Id == closed.Id && r.Status == "closed");
    }

    [Fact]
    public async Task SearchPublicAsync_WithMatchingKeyword_ReturnsOnlyPublished()
    {
        await CreateAndPublishAsync("Senior Engineer", "We build backend systems.");
        await CreateAsync("Draft Engineer", "This one stays a draft."); // draft, never published
        await CreateAndPublishAsync("Sales Manager", "No matching keyword here.");

        var result = await _service.SearchPublicAsync("engineer", 1, 20);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value!.Items);
        Assert.Equal("Senior Engineer", result.Value.Items[0].Title);
    }

    [Fact]
    public async Task SearchPublicAsync_WithNoMatch_ReturnsEmptyNotError()
    {
        await CreateAndPublishAsync("Senior Engineer", "We build backend systems.");

        var result = await _service.SearchPublicAsync("nonexistentkeyword", 1, 20);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value!.Items);
        Assert.Equal(0, result.Value.Total);
    }

    [Fact]
    public async Task ListAsync_DefaultPage_ReturnsFirst20WithMetadata()
    {
        for (var i = 0; i < 25; i++)
        {
            await CreateAndPublishAsync($"Role {i}", "Description");
        }

        var result = await _service.SearchPublicAsync(null, 1, 20);

        Assert.True(result.IsSuccess);
        Assert.Equal(20, result.Value!.Items.Count);
        Assert.Equal(1, result.Value.Page);
        Assert.Equal(20, result.Value.PageSize);
        Assert.Equal(25, result.Value.Total);
    }

    [Fact]
    public async Task SearchPublicAsync_PageBeyondLast_ReturnsEmptyWithTrueTotal()
    {
        for (var i = 0; i < 5; i++)
        {
            await CreateAndPublishAsync($"Role {i}", "Description");
        }

        var result = await _service.SearchPublicAsync(null, 99, 20);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value!.Items);
        Assert.Equal(5, result.Value.Total);
    }

    [Fact]
    public async Task SearchPublicAsync_KeywordAndPage_FiltersThenPaginates()
    {
        for (var i = 0; i < 25; i++)
        {
            await CreateAndPublishAsync($"Engineer {i}", "Backend role.");
        }
        await CreateAndPublishAsync("Sales Manager", "A different kind of role.");

        var firstPage = await _service.SearchPublicAsync("engineer", 1, 20);
        var secondPage = await _service.SearchPublicAsync("engineer", 2, 20);

        Assert.True(firstPage.IsSuccess);
        Assert.Equal(20, firstPage.Value!.Items.Count);
        Assert.Equal(25, firstPage.Value.Total);

        Assert.True(secondPage.IsSuccess);
        Assert.Equal(5, secondPage.Value!.Items.Count);
        Assert.Equal(25, secondPage.Value.Total);
    }

    [Fact]
    public async Task GetPublicByIdAsync_Published_ReturnsContent()
    {
        var published = await CreateAndPublishAsync("Senior Engineer", "We build backend systems.");

        var result = await _service.GetPublicByIdAsync(published.Id);

        Assert.True(result.IsSuccess);
        Assert.Equal("Senior Engineer", result.Value!.Title);
    }

    [Fact]
    public async Task GetPublicByIdAsync_Draft_ReturnsNotFound()
    {
        var draft = await CreateAsync();

        var result = await _service.GetPublicByIdAsync(draft.Id);

        Assert.False(result.IsSuccess);
        Assert.Equal(ResultStatus.NotFound, result.Status);
        Assert.Equal("requisition.not-found", result.ErrorCode);
    }

    [Fact]
    public async Task GetPublicByIdAsync_Closed_ReturnsNotFound()
    {
        var published = await CreateAndPublishAsync();
        await _service.CloseAsync(published.Id);

        var result = await _service.GetPublicByIdAsync(published.Id);

        Assert.False(result.IsSuccess);
        Assert.Equal(ResultStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task GetPublicByIdAsync_Missing_ReturnsNotFound()
    {
        var result = await _service.GetPublicByIdAsync(Guid.NewGuid());

        Assert.False(result.IsSuccess);
        Assert.Equal(ResultStatus.NotFound, result.Status);
        Assert.Equal("requisition.not-found", result.ErrorCode);
    }

    [Fact]
    public async Task SearchPublicAsync_PageSizeAboveMax_IsClampedTo50()
    {
        for (var i = 0; i < 55; i++)
        {
            await CreateAndPublishAsync($"Role {i}", "Description");
        }

        var result = await _service.SearchPublicAsync(null, 1, 1000);

        Assert.True(result.IsSuccess);
        Assert.Equal(50, result.Value!.PageSize);
        Assert.Equal(50, result.Value.Items.Count);
    }

    [Fact]
    public async Task PublicReads_GetByIdAndSearch_NeverOpenATransaction()
    {
        // NFR-2: public GET paths must never open a write transaction, preserving SQLite's
        // single-writer path for registration/application traffic. A DbTransactionInterceptor
        // observes every transaction EF Core actually starts (explicit or the implicit one
        // SaveChangesAsync wraps writes in) on the same underlying connection this test's
        // setup already used to create and publish a requisition.
        var published = await CreateAndPublishAsync("Senior Engineer", "We build backend systems.");

        var interceptor = new TransactionTrackingInterceptor();
        var instrumentedOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .AddInterceptors(interceptor)
            .Options;

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Requisitions:DefaultPageSize"] = "20",
                ["Requisitions:MaxPageSize"] = "50"
            })
            .Build();

        await using var instrumentedContext = new AppDbContext(instrumentedOptions);
        var instrumentedService = new RequisitionService(instrumentedContext, configuration);

        var detailResult = await instrumentedService.GetPublicByIdAsync(published.Id);
        var searchResult = await instrumentedService.SearchPublicAsync(null, 1, 20);

        Assert.True(detailResult.IsSuccess);
        Assert.True(searchResult.IsSuccess);
        Assert.False(interceptor.TransactionOpened, "GetPublicByIdAsync/SearchPublicAsync must never open a transaction (NFR-2).");
        Assert.Null(instrumentedContext.Database.CurrentTransaction);
    }

    /// <summary>
    /// Records whether EF Core ever started a database transaction (explicit or the implicit
    /// one it wraps write operations in) on the intercepted context — used by
    /// <see cref="PublicReads_GetByIdAndSearch_NeverOpenATransaction"/> to assert NFR-2.
    /// </summary>
    private sealed class TransactionTrackingInterceptor : DbTransactionInterceptor
    {
        public bool TransactionOpened { get; private set; }

        public override DbTransaction TransactionStarted(DbConnection connection, TransactionEndEventData eventData, DbTransaction result)
        {
            TransactionOpened = true;
            return base.TransactionStarted(connection, eventData, result);
        }

        public override ValueTask<DbTransaction> TransactionStartedAsync(
            DbConnection connection, TransactionEndEventData eventData, DbTransaction result, CancellationToken cancellationToken = default)
        {
            TransactionOpened = true;
            return base.TransactionStartedAsync(connection, eventData, result, cancellationToken);
        }
    }
}
