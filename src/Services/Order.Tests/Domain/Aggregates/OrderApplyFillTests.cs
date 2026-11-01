using Order.Domain;
using Order.Domain.Aggregates;
using Order.Domain.Events;
using Xunit;
using static Order.Tests.Domain.Aggregates.TestOrders;

namespace Order.Tests.Domain.Aggregates;

public class OrderApplyFillTests
{
    [Fact]
    public void ApplyFill_DuplicateExecutionId_DoesNothing()
    {
        var order = InStatus(OrderStatus.New);
        var executionId = Guid.NewGuid();
        Assert.True(Fill(order, executionId, 3, ApprovedAt).IsSuccess);
        order.ClearDomainEvents();

        var result = Fill(order, executionId, 3, Now);

        Assert.True(result.IsSuccess);
        Assert.Single(order.Fills);
        Assert.Equal(3, order.FilledQuantity);
        Assert.Equal(OrderStatus.PartiallyFilled, order.Status);
        Assert.Empty(order.GetDomainEvents());
    }

    [Fact]
    public void ApplyFill_NotApproved_ReturnsSpecificationFailure()
    {
        var order = InStatus(OrderStatus.PendingNew);

        var result = Fill(order, Guid.NewGuid(), 1, Now);

        AssertFailure(result, ErrorType.Violation, "Order must be approved before it can be filled.");
        AssertUnchanged(order, OrderStatus.PendingNew);
    }

    [Fact]
    public void ApplyFill_Overfill_ReturnsSpecificationFailure()
    {
        var order = InStatus(OrderStatus.New);

        var result = Fill(order, Guid.NewGuid(), Quantity + 1, Now);

        AssertFailure(result, ErrorType.Violation, "Fill quantity exceeds remaining order quantity.");
        AssertUnchanged(order, OrderStatus.New);
    }

    [Fact]
    public void ApplyFill_EmptyExecutionId_ReturnsFillCreationFailure()
    {
        var order = InStatus(OrderStatus.New);

        var result = Fill(order, Guid.Empty, 1, Now);

        AssertFailure(result, ErrorType.Validation, "ExecutionId cannot be empty.");
        AssertUnchanged(order, OrderStatus.New);
    }

    [Theory]
    [InlineData(OrderStatus.New, OrderStatus.PartiallyFilled)]
    [InlineData(OrderStatus.PartiallyFilled, OrderStatus.PartiallyFilled)]
    [InlineData(OrderStatus.PendingCancel, OrderStatus.PendingCancel)]
    public void ApplyFill_PartialFill_UpdatesStatusAndRaisesFillRecorded(OrderStatus status, OrderStatus expected)
    {
        var order = InStatus(status);
        var filledBefore = order.FilledQuantity;
        var executionId = Guid.NewGuid();

        var result = Fill(order, executionId, 1, Now);

        Assert.True(result.IsSuccess);
        Assert.Equal(expected, order.Status);
        Assert.Equal(filledBefore + 1, order.FilledQuantity);
        Assert.Contains(order.Fills, f => f.ExecutionId == executionId);
        Assert.Null(order.FilledAt);
        var recorded = Assert.IsType<FillRecorded>(Assert.Single(order.GetDomainEvents()));
        Assert.Equal(order.Id, recorded.OrderId);
        Assert.Equal(executionId, recorded.ExecutionId);
        Assert.Equal(1, recorded.Quantity.Value);
        Assert.Equal(Now, recorded.FilledAt);
    }

    [Fact]
    public void ApplyFill_FillLeavingOneRemaining_StaysPartiallyFilled()
    {
        var order = InStatus(OrderStatus.New);

        var result = Fill(order, Guid.NewGuid(), Quantity - 1, Now);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, order.Remaining);
        Assert.Equal(Quantity - 1, order.FilledQuantity);
        Assert.Equal(OrderStatus.PartiallyFilled, order.Status);
        Assert.Null(order.FilledAt);
        Assert.IsType<FillRecorded>(Assert.Single(order.GetDomainEvents()));
    }

    [Theory]
    [InlineData(OrderStatus.New)]
    [InlineData(OrderStatus.PartiallyFilled)]
    [InlineData(OrderStatus.PendingCancel)]
    public void ApplyFill_CompletingFill_SetsFilledAndRaisesFillRecordedThenOrderFilled(OrderStatus status)
    {
        var order = InStatus(status);
        var executionId = Guid.NewGuid();

        var result = Fill(order, executionId, order.Remaining, Now);

        Assert.True(result.IsSuccess);
        Assert.Equal(OrderStatus.Filled, order.Status);
        Assert.Equal(0, order.Remaining);
        Assert.Equal(Now, order.FilledAt);
        Assert.Collection(order.GetDomainEvents(),
            e => Assert.Equal(executionId, Assert.IsType<FillRecorded>(e).ExecutionId),
            e =>
            {
                var filled = Assert.IsType<OrderFilled>(e);
                Assert.Equal(order.Id, filled.OrderId);
                Assert.Equal(Quantity, filled.Quantity);
                Assert.Equal(Now, filled.FilledAt);
            });
    }

    private static void AssertUnchanged(Order.Domain.Aggregates.Order order, OrderStatus status)
    {
        Assert.Equal(status, order.Status);
        Assert.Empty(order.Fills);
        Assert.Equal(Quantity, order.Remaining);
        Assert.Empty(order.GetDomainEvents());
    }
}