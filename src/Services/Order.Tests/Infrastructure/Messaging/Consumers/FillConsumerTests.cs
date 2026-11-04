using Messaging.Abstractions;
using Order.Domain.Aggregates;
using Order.Infrastructure.Messaging.Consumers;
using Order.Infrastructure.Messaging.Reports;
using Order.Tests.Domain.Aggregates;
using Order.Tests.Infrastructure;
using Xunit;

namespace Order.Tests.Infrastructure.Messaging.Consumers;

public class FillConsumerTests(MssqlFixture fixture) : IClassFixture<MssqlFixture>, IAsyncDisposable
{
    private readonly OrderTestScope _scope = OrderTestScope.Create(fixture);

    public ValueTask DisposeAsync() => _scope.DisposeAsync();

    [Fact]
    public async Task ConsumeAsync_KnownOrder_ReturnsSuccessAndAppliesReport()
    {
        var order = await _scope.SeedAsync(TestOrders.InStatus(OrderStatus.New));
        var executionId = Guid.NewGuid();

        var outcome = await _scope.ConsumeAsync<FillConsumer, FillReport>(
            new FillReport(order.Id, executionId, 3, 12.5m, TestOrders.Now));

        Assert.Equal(MessageConsumeResult.Success, outcome.Kind);
        var updated = await _scope.FindAsync(order.Id);
        Assert.Equal(OrderStatus.PartiallyFilled, updated!.Status);
        var fill = Assert.Single(updated.Fills);
        Assert.Equal(executionId, fill.ExecutionId);
        Assert.Equal(3, fill.Quantity.Value);
        Assert.Equal(12.5m, fill.Price.Value);
        Assert.Equal(TestOrders.Now, fill.FilledAt);
    }

    [Fact]
    public async Task ConsumeAsync_UnknownOrder_ReturnsRetry()
    {
        var outcome = await _scope.ConsumeAsync<FillConsumer, FillReport>(
            new FillReport(Guid.NewGuid(), Guid.NewGuid(), 1, 10m, TestOrders.Now));

        Assert.Equal(MessageConsumeResult.Retry, outcome.Kind);
    }

    [Fact]
    public async Task ConsumeAsync_InvalidReport_ReturnsDeadLetter()
    {
        var order = await _scope.SeedAsync(TestOrders.InStatus(OrderStatus.New));

        var outcome = await _scope.ConsumeAsync<FillConsumer, FillReport>(
            new FillReport(order.Id, Guid.NewGuid(), 0, 10m, TestOrders.Now));

        Assert.Equal(MessageConsumeResult.DeadLetter, outcome.Kind);
        var updated = await _scope.FindAsync(order.Id);
        Assert.Empty(updated!.Fills);
    }

    [Fact]
    public async Task ConsumeAsync_InvalidTransition_ReturnsRetry()
    {
        var order = await _scope.SeedAsync(TestOrders.InStatus(OrderStatus.Filled));

        var outcome = await _scope.ConsumeAsync<FillConsumer, FillReport>(
            new FillReport(order.Id, Guid.NewGuid(), 1, 10m, TestOrders.Now));

        Assert.Equal(MessageConsumeResult.Retry, outcome.Kind);
        var updated = await _scope.FindAsync(order.Id);
        Assert.Single(updated!.Fills);
    }
}
