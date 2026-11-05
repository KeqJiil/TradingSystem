using Messaging.Abstractions;
using Order.Domain.Aggregates;
using Order.Infrastructure.Messaging.Consumers;
using Order.Infrastructure.Messaging.Reports;
using Order.Tests.Domain.Aggregates;
using Order.Tests.Infrastructure;
using Xunit;

namespace Order.Tests.Infrastructure.Messaging.Consumers;

public class OrderExpiredConsumerTests(MssqlFixture fixture) : IClassFixture<MssqlFixture>, IAsyncDisposable
{
    private readonly OrderTestScope _scope = OrderTestScope.Create(fixture);

    public ValueTask DisposeAsync() => _scope.DisposeAsync();

    [Fact]
    public async Task ConsumeAsync_KnownOrder_ReturnsSuccessAndAppliesReport()
    {
        var order = await _scope.SeedAsync(TestOrders.InStatus(OrderStatus.New));

        var outcome = await _scope.ConsumeAsync<OrderExpiredConsumer, OrderExpiredReport>(
            new OrderExpiredReport(order.Id, TestOrders.Now));

        Assert.Equal(MessageConsumeResult.Success, outcome.Kind);
        var updated = await _scope.FindAsync(order.Id);
        Assert.Equal(OrderStatus.Expired, updated!.Status);
        Assert.Equal(TestOrders.Now, updated.ExpiredAt);
    }

    [Fact]
    public async Task ConsumeAsync_UnknownOrder_ReturnsRetry()
    {
        var outcome = await _scope.ConsumeAsync<OrderExpiredConsumer, OrderExpiredReport>(
            new OrderExpiredReport(Guid.NewGuid(), TestOrders.Now));

        Assert.Equal(MessageConsumeResult.Retry, outcome.Kind);
    }

    [Fact]
    public async Task ConsumeAsync_InvalidTransition_ReturnsDeadLetter()
    {
        var order = await _scope.SeedAsync(TestOrders.InStatus(OrderStatus.Filled));

        var outcome = await _scope.ConsumeAsync<OrderExpiredConsumer, OrderExpiredReport>(
            new OrderExpiredReport(order.Id, TestOrders.Now));

        Assert.Equal(MessageConsumeResult.DeadLetter, outcome.Kind);
        var updated = await _scope.FindAsync(order.Id);
        Assert.Equal(OrderStatus.Filled, updated!.Status);
    }
}
