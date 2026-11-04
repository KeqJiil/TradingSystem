using Order.Application.UseCases.ApplyFill;
using Order.Domain;
using Order.Domain.Aggregates;
using Order.Infrastructure.Messaging.Events;
using Order.Tests.Domain.Aggregates;
using Order.Tests.Infrastructure;
using Xunit;

namespace Order.Tests.Application.UseCases;

public class ApplyFillCommandHandlerTests(MssqlFixture fixture) : IClassFixture<MssqlFixture>, IAsyncDisposable
{
    private readonly OrderTestScope _scope = OrderTestScope.Create(fixture);

    public ValueTask DisposeAsync() => _scope.DisposeAsync();

    [Fact]
    public async Task Handle_ShouldReturnNotFound_WhenOrderDoesNotExist()
    {
        var result = await _scope.SendAsync(Fill(Guid.NewGuid(), Guid.NewGuid(), 1));

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.NotFound, result.Error.Type);
    }

    [Fact]
    public async Task Handle_ShouldReturnValidationError_WhenEntryDataIsInvalid()
    {
        var order = await _scope.SeedAsync(TestOrders.InStatus(OrderStatus.New));

        var result = await _scope.SendAsync(Fill(order.Id, Guid.NewGuid(), 0));

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
        var updated = await _scope.FindAsync(order.Id);
        Assert.Equal(0, updated!.FilledQuantity);
        Assert.Empty(updated.Fills);
    }

    [Fact]
    public async Task Handle_ShouldReturnViolation_WhenOrderIsNotFillable()
    {
        var order = await _scope.SeedAsync(TestOrders.InStatus(OrderStatus.Filled));

        var result = await _scope.SendAsync(Fill(order.Id, Guid.NewGuid(), 1));

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Violation, result.Error.Type);
        var updated = await _scope.FindAsync(order.Id);
        Assert.Equal(TestOrders.Quantity, updated!.FilledQuantity);
        Assert.Single(updated.Fills);
    }

    [Fact]
    public async Task Handle_ShouldPersistFillAndPartiallyFilled_WhenFillIsPartial()
    {
        var order = await _scope.SeedAsync(TestOrders.InStatus(OrderStatus.New));
        var executionId = Guid.NewGuid();

        var result = await _scope.SendAsync(Fill(order.Id, executionId, 3));

        Assert.True(result.IsSuccess);
        var updated = await _scope.FindAsync(order.Id);
        Assert.Equal(OrderStatus.PartiallyFilled, updated!.Status);
        Assert.Equal(3, updated.FilledQuantity);
        Assert.Null(updated.FilledAt);

        var fill = Assert.Single(updated.Fills);
        Assert.Equal(executionId, fill.ExecutionId);
        Assert.Equal(3, fill.Quantity.Value);
        Assert.Equal(10m, fill.Price.Value);
        Assert.Equal(TestOrders.Now, fill.FilledAt);

        Assert.Empty(await _scope.Published.OfTypeAsync<OrderFilledEvent>(order.Id.ToString()));
    }

    [Fact]
    public async Task Handle_ShouldPersistFilledAndPublishOrderFilled_WhenFillCompletesOrder()
    {
        var order = await _scope.SeedAsync(TestOrders.InStatus(OrderStatus.PartiallyFilled));

        var result = await _scope.SendAsync(Fill(order.Id, Guid.NewGuid(), TestOrders.Quantity - 1));

        Assert.True(result.IsSuccess);
        var updated = await _scope.FindAsync(order.Id);
        Assert.Equal(OrderStatus.Filled, updated!.Status);
        Assert.Equal(TestOrders.Quantity, updated.FilledQuantity);
        Assert.Equal(TestOrders.Now, updated.FilledAt);
        Assert.Equal(2, updated.Fills.Count);

        var published = Assert.Single(await _scope.Published.OfTypeAsync<OrderFilledEvent>(order.Id.ToString()));
        Assert.Equal(TestOrders.Quantity, published.Message.Quantity);
        Assert.Equal(TestOrders.Now, published.Message.OccurredAt);
    }

    [Fact]
    public async Task Handle_ShouldReturnSuccessAndApplyOnce_WhenSameExecutionIsDeliveredTwice()
    {
        var order = await _scope.SeedAsync(TestOrders.InStatus(OrderStatus.New));
        var command = Fill(order.Id, Guid.NewGuid(), TestOrders.Quantity);

        var first = await _scope.SendAsync(command);
        var second = await _scope.SendAsync(command);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        var updated = await _scope.FindAsync(order.Id);
        Assert.Equal(TestOrders.Quantity, updated!.FilledQuantity);
        Assert.Single(updated.Fills);
        Assert.Single(await _scope.Published.OfTypeAsync<OrderFilledEvent>(order.Id.ToString()));
    }

    private static ApplyFillCommand Fill(Guid orderId, Guid executionId, int quantity) =>
        new(orderId, executionId, quantity, 10m, TestOrders.Now);
}
