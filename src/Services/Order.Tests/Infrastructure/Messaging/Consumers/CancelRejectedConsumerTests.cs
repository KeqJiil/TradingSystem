using Messaging.Abstractions;
using Order.Domain.Aggregates;
using Order.Infrastructure.Messaging.Consumers;
using Order.Infrastructure.Messaging.Reports;
using Order.Tests.Domain.Aggregates;
using Xunit;

namespace Order.Tests.Infrastructure.Messaging.Consumers;

public class CancelRejectedConsumerTests(MssqlFixture fixture) : IClassFixture<MssqlFixture>, IAsyncDisposable
{
    private readonly OrderTestScope _scope = OrderTestScope.Create(fixture);

    public ValueTask DisposeAsync() => _scope.DisposeAsync();

    [Fact]
    public async Task ConsumeAsync_KnownOrder_ReturnsSuccessAndAppliesReport()
    {
        var order = await _scope.SeedAsync(TestOrders.InStatus(OrderStatus.PendingCancel));

        var outcome = await _scope.ConsumeAsync<CancelRejectedConsumer, CancelRejectedReport>(
            new CancelRejectedReport(order.Id, TestOrders.Reason, TestOrders.Now));

        Assert.Equal(MessageConsumeResult.Success, outcome.Kind);
        var updated = await _scope.FindAsync(order.Id);
        Assert.Equal(OrderStatus.New, updated!.Status);
    }

    [Fact]
    public async Task ConsumeAsync_UnknownOrder_ReturnsRetry()
    {
        var outcome = await _scope.ConsumeAsync<CancelRejectedConsumer, CancelRejectedReport>(
            new CancelRejectedReport(Guid.NewGuid(), TestOrders.Reason, TestOrders.Now));

        Assert.Equal(MessageConsumeResult.Retry, outcome.Kind);
    }

    [Fact]
    public async Task ConsumeAsync_InvalidTransition_ReturnsDeadLetter()
    {
        var order = await _scope.SeedAsync(TestOrders.InStatus(OrderStatus.New));

        var outcome = await _scope.ConsumeAsync<CancelRejectedConsumer, CancelRejectedReport>(
            new CancelRejectedReport(order.Id, TestOrders.Reason, TestOrders.Now));

        Assert.Equal(MessageConsumeResult.DeadLetter, outcome.Kind);
        var updated = await _scope.FindAsync(order.Id);
        Assert.Equal(OrderStatus.New, updated!.Status);
    }
}
