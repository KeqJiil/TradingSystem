using Order.Application.UseCases.RenameStock;
using Order.Domain;
using Order.Tests.Domain.Aggregates;
using Order.Tests.Infrastructure;
using Xunit;

namespace Order.Tests.Application.UseCases;

public class RenameStockCommandHandlerTests(MssqlFixture fixture) : IClassFixture<MssqlFixture>, IAsyncDisposable
{
    private readonly OrderTestScope _scope = OrderTestScope.Create(fixture);

    public ValueTask DisposeAsync() => _scope.DisposeAsync();

    [Fact]
    public async Task Handle_UnknownStock_ReturnsNotFound()
    {
        var result = await _scope.SendAsync(new RenameStockCommand(Guid.CreateVersion7(), "MSFT", 1));

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.NotFound, result.Error.Type);
    }

    [Fact]
    public async Task Handle_NewerVersion_UpdatesRow()
    {
        var stock = await _scope.SeedStockAsync(TestStocks.Create());

        var result = await _scope.SendAsync(new RenameStockCommand(stock.Id, "MSFT", 2));

        Assert.True(result.IsSuccess);
        var row = await _scope.FindStockAsync(stock.Id);
        Assert.Equal("MSFT", row!.Name);
        Assert.Equal(2, row.NameVersion);
    }

    [Fact]
    public async Task Handle_StaleVersion_LeavesRowUnchanged()
    {
        var seeded = TestStocks.Create();
        seeded.Rename("MSFT", 5);
        await _scope.SeedStockAsync(seeded);

        var result = await _scope.SendAsync(new RenameStockCommand(seeded.Id, "GOOG", 3));

        Assert.True(result.IsSuccess);
        var row = await _scope.FindStockAsync(seeded.Id);
        Assert.Equal("MSFT", row!.Name);
        Assert.Equal(5, row.NameVersion);
    }
}
