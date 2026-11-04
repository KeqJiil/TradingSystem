using Order.Application.UseCases.ExpireOrder;
using Order.Domain;
using Order.Domain.Aggregates;
using Order.Infrastructure.Messaging.Events;
using Order.Tests.Domain.Aggregates;
using Order.Tests.Infrastructure;
using Xunit;

namespace Order.Tests.Application.UseCases;

public class ExpireOrderCommandHandlerTests(MssqlFixture fixture) : IClassFixture<MssqlFixture>, IAsyncDisposable
{
    private readonly OrderTestScope _scope = OrderTestScope.Create(fixture);

    public ValueTask DisposeAsync() => _scope.DisposeAsync();

    [Fact]
    public async Task Handle_OrderDoesNotExist_ReturnsNotFound()
    {
        var result = await _scope.SendAsync(new ExpireOrderCommand(Guid.NewGuid(), TestOrders.Now));

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.NotFound, result.Error.Type);
    }

    [Fact]
    public async Task Handle_NewOrder_PersistsExpiredWithExpiredAtAndPublishesOrderExpired()
    {
        var order = await _scope.SeedAsync(TestOrders.InStatus(OrderStatus.New));

        var result = await _scope.SendAsync(new ExpireOrderCommand(order.Id, TestOrders.Now));

        Assert.True(result.IsSuccess);
        var updated = await _scope.FindAsync(order.Id);
        Assert.Equal(OrderStatus.Expired, updated!.Status);
        Assert.Equal(TestOrders.Now, updated.ExpiredAt);

        var published = Assert.Single(await _scope.Published.OfTypeAsync<OrderExpiredEvent>(order.Id.ToString()));
        Assert.Equal(TestOrders.Now, published.Message.OccurredAt);
    }

    [Fact]
    public async Task Handle_PartiallyFilledOrder_PersistsExpiredAndKeepsFilledQuantity()
    {
        var order = await _scope.SeedAsync(TestOrders.InStatus(OrderStatus.PartiallyFilled));

        var result = await _scope.SendAsync(new ExpireOrderCommand(order.Id, TestOrders.Now));

        Assert.True(result.IsSuccess);
        var updated = await _scope.FindAsync(order.Id);
        Assert.Equal(OrderStatus.Expired, updated!.Status);
        Assert.Equal(order.FilledQuantity, updated.FilledQuantity);
        Assert.Single(updated.Fills);
    }

    [Fact]
    public async Task Handle_AlreadyExpired_ReturnsSuccessAndPublishesNothing()
    {
        var order = await _scope.SeedAsync(TestOrders.InStatus(OrderStatus.Expired));

        var result = await _scope.SendAsync(new ExpireOrderCommand(order.Id, TestOrders.Now));

        Assert.True(result.IsSuccess);
        var updated = await _scope.FindAsync(order.Id);
        Assert.Equal(TestOrders.ApprovedAt, updated!.ExpiredAt);
        Assert.Empty(await _scope.Published.OfTypeAsync<OrderExpiredEvent>(order.Id.ToString()));
    }

    [Fact]
    public async Task Handle_FilledOrder_ReturnsViolationAndKeepsFilled()
    {
        var order = await _scope.SeedAsync(TestOrders.InStatus(OrderStatus.Filled));

        var result = await _scope.SendAsync(new ExpireOrderCommand(order.Id, TestOrders.Now));

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Violation, result.Error.Type);
        var updated = await _scope.FindAsync(order.Id);
        Assert.Equal(OrderStatus.Filled, updated!.Status);
    }
}
