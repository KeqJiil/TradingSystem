using Messaging.Abstractions;
using Order.Infrastructure.Messaging.Consumers;
using Order.Infrastructure.Messaging.StockEvents;
using Order.Tests.Infrastructure;
using Xunit;

namespace Order.Tests.Infrastructure.Messaging.Consumers;

public class StockCreatedConsumerTests(MssqlFixture fixture) : IClassFixture<MssqlFixture>, IAsyncDisposable
{
    private readonly OrderTestScope _scope = OrderTestScope.Create(fixture);

    public ValueTask DisposeAsync() => _scope.DisposeAsync();

    [Fact]
    public async Task ConsumeAsync_ValidEvent_ReturnsSuccessAndPersistsRow()
    {
        var stockId = Guid.CreateVersion7();

        var outcome = await ConsumeAsync(CreatedEvent(stockId));

        Assert.Equal(MessageConsumeResult.Success, outcome.Kind);
        var stock = await _scope.FindStockAsync(stockId);
        Assert.NotNull(stock);
        Assert.Equal("AAPL", stock.Name);
        Assert.True(stock.IsOpenToTrade);
        Assert.Equal(new TimeOnly(9, 30), stock.TradingStart);
        Assert.Equal(new TimeOnly(16, 0), stock.TradingEnd);
    }

    [Fact]
    public async Task ConsumeAsync_EmptyStockId_ReturnsDeadLetter()
    {
        var outcome = await ConsumeAsync(CreatedEvent(Guid.Empty));

        Assert.Equal(MessageConsumeResult.DeadLetter, outcome.Kind);
    }

    [Fact]
    public async Task ConsumeAsync_DuplicateEvent_ReturnsSuccess()
    {
        var stockId = Guid.CreateVersion7();
        await ConsumeAsync(CreatedEvent(stockId));

        var outcome = await ConsumeAsync(CreatedEvent(stockId));

        Assert.Equal(MessageConsumeResult.Success, outcome.Kind);
        Assert.NotNull(await _scope.FindStockAsync(stockId));
    }

    private Task<ConsumeOutcome> ConsumeAsync(StockCreatedEvent evt) =>
        _scope.ConsumeAsync<StockCreatedConsumer, StockCreatedEvent>(evt);

    private static StockCreatedEvent CreatedEvent(Guid stockId) =>
        new(stockId, "AAPL", true, new TimeOnly(9, 30), new TimeOnly(16, 0));
}
