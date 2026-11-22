using Google.Protobuf.WellKnownTypes;
using Messaging.Abstractions;
using Order.Infrastructure.Messaging.Consumers;
using Order.Tests.Domain.Aggregates;
using Order.Tests.Infrastructure;
using Xunit;
using TradingSystem.Contracts.ProtobufClasses.Stock;

namespace Order.Tests.Infrastructure.Messaging.Consumers;

public class TimeChangedConsumerTests(MssqlFixture fixture) : IClassFixture<MssqlFixture>, IAsyncDisposable
{
    private static readonly TimeOnly NewOpen = new(10, 0);
    private static readonly TimeOnly NewClose = new(15, 0);

    private readonly OrderTestScope _scope = OrderTestScope.Create(fixture);

    public ValueTask DisposeAsync() => _scope.DisposeAsync();

    [Fact]
    public async Task ConsumeAsync_ValidEvent_ReturnsSuccessAndUpdatesRow()
    {
        var stock = await _scope.SeedStockAsync(TestStocks.Create());

        var outcome = await ConsumeAsync(TimeChanged(stock.Id.ToString()));

        Assert.Equal(MessageConsumeResult.Success, outcome.Kind);
        var row = await _scope.FindStockAsync(stock.Id);
        Assert.Equal(NewOpen, row!.TradingStart);
        Assert.Equal(NewClose, row.TradingEnd);
        Assert.Equal(2, row.TradingTimeVersion);
    }

    [Fact]
    public async Task ConsumeAsync_UnknownStock_ReturnsRetry()
    {
        var outcome = await ConsumeAsync(TimeChanged(Guid.CreateVersion7().ToString()));

        Assert.Equal(MessageConsumeResult.Retry, outcome.Kind);
    }

    [Fact]
    public async Task ConsumeAsync_NonGuidStockId_ReturnsDeadLetter()
    {
        var outcome = await ConsumeAsync(TimeChanged("not-a-guid"));

        Assert.Equal(MessageConsumeResult.DeadLetter, outcome.Kind);
    }

    [Fact]
    public async Task ConsumeAsync_NullTradingTime_ReturnsDeadLetter()
    {
        var evt = TimeChanged(Guid.CreateVersion7().ToString());
        evt.TradingCloseTime = null;

        var outcome = await ConsumeAsync(evt);

        Assert.Equal(MessageConsumeResult.DeadLetter, outcome.Kind);
    }

    private Task<ConsumeOutcome> ConsumeAsync(TimeChangedEvent evt) =>
        _scope.ConsumeAsync<TimeChangedConsumer, TimeChangedEvent>(evt);

    private static TimeChangedEvent TimeChanged(string aggregateId) => new()
    {
        AggregateId = aggregateId,
        TradingStartTime = Duration.FromTimeSpan(NewOpen.ToTimeSpan()),
        TradingCloseTime = Duration.FromTimeSpan(NewClose.ToTimeSpan()),
        Version = 2
    };
}
