using Google.Protobuf.WellKnownTypes;
using Messaging.Abstractions;
using Order.Infrastructure.Messaging.Consumers;
using Order.Tests.Infrastructure;
using Xunit;
using TradingSystem.Contracts.ProtobufClasses.Stock;

namespace Order.Tests.Infrastructure.Messaging.Consumers;

public class StockCreatedConsumerTests(MssqlFixture fixture) : IClassFixture<MssqlFixture>, IAsyncDisposable
{
    private readonly OrderTestScope _scope = OrderTestScope.Create(fixture);

    public ValueTask DisposeAsync() => _scope.DisposeAsync();

    [Fact]
    public async Task ConsumeAsync_ValidEvent_ReturnsSuccessAndPersistsRow()
    {
        var stockId = Guid.CreateVersion7();

        var outcome = await ConsumeAsync(CreatedEvent(stockId.ToString()));

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
        var outcome = await ConsumeAsync(CreatedEvent(Guid.Empty.ToString()));

        Assert.Equal(MessageConsumeResult.DeadLetter, outcome.Kind);
    }

    [Fact]
    public async Task ConsumeAsync_NonGuidStockId_ReturnsDeadLetter()
    {
        var outcome = await ConsumeAsync(CreatedEvent("not-a-guid"));

        Assert.Equal(MessageConsumeResult.DeadLetter, outcome.Kind);
    }

    [Fact]
    public async Task ConsumeAsync_TradingTimeAt24Hours_ReturnsDeadLetter()
    {
        var evt = CreatedEvent(Guid.CreateVersion7().ToString());
        evt.TradingCloseTime = Duration.FromTimeSpan(TimeSpan.FromHours(24));

        var outcome = await ConsumeAsync(evt);

        Assert.Equal(MessageConsumeResult.DeadLetter, outcome.Kind);
    }

    [Fact]
    public async Task ConsumeAsync_NullTradingTime_ReturnsDeadLetter()
    {
        var evt = CreatedEvent(Guid.CreateVersion7().ToString());
        evt.TradingStartTime = null;

        var outcome = await ConsumeAsync(evt);

        Assert.Equal(MessageConsumeResult.DeadLetter, outcome.Kind);
    }

    [Fact]
    public async Task ConsumeAsync_DuplicateEvent_ReturnsSuccess()
    {
        var stockId = Guid.CreateVersion7();
        await ConsumeAsync(CreatedEvent(stockId.ToString()));

        var outcome = await ConsumeAsync(CreatedEvent(stockId.ToString()));

        Assert.Equal(MessageConsumeResult.Success, outcome.Kind);
        Assert.NotNull(await _scope.FindStockAsync(stockId));
    }

    private Task<ConsumeOutcome> ConsumeAsync(StockCreatedEvent evt) =>
        _scope.ConsumeAsync<StockCreatedConsumer, StockCreatedEvent>(evt);

    private static StockCreatedEvent CreatedEvent(string aggregateId) => new()
    {
        AggregateId = aggregateId,
        Name = "AAPL",
        IsOpenToTrade = true,
        Currency = "USD",
        TradingStartTime = Duration.FromTimeSpan(new TimeSpan(9, 30, 0)),
        TradingCloseTime = Duration.FromTimeSpan(new TimeSpan(16, 0, 0))
    };
}
