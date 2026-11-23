using Messaging.Abstractions;
using Order.Infrastructure.Messaging.Consumers;
using Order.Tests.Domain.Aggregates;
using Order.Tests.Infrastructure;
using Xunit;
using TradingSystem.Contracts.ProtobufClasses.Stock;

namespace Order.Tests.Infrastructure.Messaging.Consumers;

public class StockToggledStatusConsumerTests(MssqlFixture fixture) : IClassFixture<MssqlFixture>, IAsyncDisposable
{
    private readonly OrderTestScope _scope = OrderTestScope.Create(fixture);

    public ValueTask DisposeAsync() => _scope.DisposeAsync();

    [Fact]
    public async Task ConsumeAsync_ValidEvent_ReturnsSuccessAndUpdatesRow()
    {
        var stock = await _scope.SeedStockAsync(TestStocks.Create());

        var outcome = await ConsumeAsync(Toggled(stock.Id.ToString(), false, 2));

        Assert.Equal(MessageConsumeResult.Success, outcome.Kind);
        var row = await _scope.FindStockAsync(stock.Id);
        Assert.False(row!.IsOpenToTrade);
        Assert.Equal(2, row.StatusVersion);
    }

    [Fact]
    public async Task ConsumeAsync_UnknownStock_ReturnsRetry()
    {
        var outcome = await ConsumeAsync(Toggled(Guid.CreateVersion7().ToString(), false, 1));

        Assert.Equal(MessageConsumeResult.Retry, outcome.Kind);
    }

    [Fact]
    public async Task ConsumeAsync_NonGuidStockId_ReturnsDeadLetter()
    {
        var outcome = await ConsumeAsync(Toggled("not-a-guid", false, 1));

        Assert.Equal(MessageConsumeResult.DeadLetter, outcome.Kind);
    }

    private Task<ConsumeOutcome> ConsumeAsync(StockToggledStatusEvent evt) =>
        _scope.ConsumeAsync<StockToggledStatusConsumer, StockToggledStatusEvent>(evt);

    private static StockToggledStatusEvent Toggled(string aggregateId, bool isOpenToTrade, long version) => new()
    {
        AggregateId = aggregateId,
        IsOpenToTrade = isOpenToTrade,
        StatusVersion = version
    };
}
