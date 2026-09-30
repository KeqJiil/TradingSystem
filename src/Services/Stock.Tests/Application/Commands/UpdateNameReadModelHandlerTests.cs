using Dapper;
using Stock.Application.Commands.UpdateNameReadModel;
using Stock.Application.Exceptions;
using Stock.Infrastructure.Persistence.Implementations;
using Stock.Tests.Infrastructure;
using Xunit;

namespace Stock.Tests.Application.Commands;

public class UpdateNameReadModelHandlerTests : IClassFixture<MssqlFixture>, IAsyncLifetime
{
    private readonly UpdateNameReadModelHandler _handler;
    private readonly TestDbContext _dbContext;

    public UpdateNameReadModelHandlerTests(MssqlFixture fixture)
    {
        _dbContext = new TestDbContext(fixture.ConnectionString);
        _handler = new UpdateNameReadModelHandler(new StockReadModelWriter(_dbContext));
    }

    public async Task InitializeAsync()
    {
        await _dbContext.EnsureConnectionOpenAsync(CancellationToken.None);
        await TestDatabase.ResetAsync(_dbContext);
    }

    public async Task DisposeAsync()
    {
        await _dbContext.Connection.CloseAsync();
    }

    [Fact]
    public async Task Handle_ShouldSetNameAndNameVersion()
    {
        var aggregateId = Guid.NewGuid();
        await ReadModelSeed.Projection(_dbContext, aggregateId);

        await _handler.Handle(new UpdateNameReadModelCommand(aggregateId, "NewName", 1), CancellationToken.None);

        Assert.Equal(("NewName", 1L), await ReadName(aggregateId));
    }

    [Fact]
    public async Task Handle_SameVersionTwice_ShouldKeepFirstApplied()
    {
        var aggregateId = Guid.NewGuid();
        await ReadModelSeed.Projection(_dbContext, aggregateId);

        await _handler.Handle(new UpdateNameReadModelCommand(aggregateId, "First", 1), CancellationToken.None);
        await _handler.Handle(new UpdateNameReadModelCommand(aggregateId, "Second", 1), CancellationToken.None);

        Assert.Equal(("First", 1L), await ReadName(aggregateId));
    }

    [Fact]
    public async Task Handle_OlderVersionAfterNewer_ShouldBeIgnored()
    {
        var aggregateId = Guid.NewGuid();
        await ReadModelSeed.Projection(_dbContext, aggregateId);

        await _handler.Handle(new UpdateNameReadModelCommand(aggregateId, "Newer", 2), CancellationToken.None);
        await _handler.Handle(new UpdateNameReadModelCommand(aggregateId, "Older", 1), CancellationToken.None);

        Assert.Equal(("Newer", 2L), await ReadName(aggregateId));
    }

    [Fact]
    public async Task Handle_ShouldThrowReadModelNotFound_WhenReadModelDoesNotExist()
    {
        var aggregateId = Guid.NewGuid();

        var exception = await Assert.ThrowsAsync<ReadModelNotFoundException>(() =>
            _handler.Handle(new UpdateNameReadModelCommand(aggregateId, "NewName", 1), CancellationToken.None));

        Assert.Equal(aggregateId, exception.AggregateId);
    }

    private Task<(string Name, long NameVersion)> ReadName(Guid aggregateId)
    {
        return _dbContext.Connection.QuerySingleAsync<(string Name, long NameVersion)>(
            "SELECT name, name_version FROM stock_data_projection WHERE aggregate_id = @Id",
            new { Id = aggregateId });
    }
}
