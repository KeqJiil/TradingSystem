using Messaging.Abstractions;
using Order.Infrastructure.Messaging.Consumers;
using Order.Tests.Domain.Aggregates;
using Order.Tests.Infrastructure;
using Xunit;
using TradingSystem.Contracts.ProtobufClasses.Stock;

namespace Order.Tests.Infrastructure.Messaging.Consumers;

public class NameChangedConsumerTests(MssqlFixture fixture) : IClassFixture<MssqlFixture>, IAsyncDisposable
{
    private readonly OrderTestScope _scope = OrderTestScope.Create(fixture);

    public ValueTask DisposeAsync() => _scope.DisposeAsync();

    [Fact]
    public async Task ConsumeAsync_ValidEvent_ReturnsSuccessAndUpdatesRow()
    {
        var stock = await _scope.SeedStockAsync(TestStocks.Create());

        var outcome = await ConsumeAsync(NameChanged(stock.Id.ToString(), "MSFT", 2));

        Assert.Equal(MessageConsumeResult.Success, outcome.Kind);
        var row = await _scope.FindStockAsync(stock.Id);
        Assert.Equal("MSFT", row!.Name);
        Assert.Equal(2, row.NameVersion);
    }

    [Fact]
    public async Task ConsumeAsync_UnknownStock_ReturnsRetry()
    {
        var outcome = await ConsumeAsync(NameChanged(Guid.CreateVersion7().ToString(), "MSFT", 1));

        Assert.Equal(MessageConsumeResult.Retry, outcome.Kind);
    }

    [Fact]
    public async Task ConsumeAsync_NonGuidStockId_ReturnsDeadLetter()
    {
        var outcome = await ConsumeAsync(NameChanged("not-a-guid", "MSFT", 1));

        Assert.Equal(MessageConsumeResult.DeadLetter, outcome.Kind);
    }

    private Task<ConsumeOutcome> ConsumeAsync(NameChangedEvent evt) =>
        _scope.ConsumeAsync<NameChangedConsumer, NameChangedEvent>(evt);

    private static NameChangedEvent NameChanged(string aggregateId, string name, long version) => new()
    {
        AggregateId = aggregateId,
        Name = name,
        Version = version
    };
}
