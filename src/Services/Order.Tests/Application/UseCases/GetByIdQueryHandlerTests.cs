using Order.Application.UseCases.GetById;
using Order.Domain.Aggregates;
using Order.Tests.Domain.Aggregates;
using Order.Tests.Infrastructure;
using Xunit;

namespace Order.Tests.Application.UseCases;

public class GetByIdQueryHandlerTests(MssqlFixture fixture) : IClassFixture<MssqlFixture>, IAsyncDisposable
{
    private readonly OrderTestScope _scope = OrderTestScope.Create(fixture);

    public ValueTask DisposeAsync() => _scope.DisposeAsync();

    [Fact]
    public async Task Handle_OwnOrder_ReturnsDetailsWithTermsAndFills()
    {
        var order = await _scope.SeedAsync(TestOrders.InStatus(OrderStatus.PartiallyFilled));

        var details = await _scope.SendAsync(new GetByIdQuery(order.Id, order.UserId));

        Assert.NotNull(details);
        Assert.Equal(order.Id, details.Order.Id);
        Assert.Equal(OrderStatus.PartiallyFilled, details.Order.Status);
        Assert.Equal(TestOrders.Quantity, details.Order.Quantity);
        Assert.Equal(1, details.Order.FilledQuantity);
        Assert.Equal(order.OrderTerms.TimeInForce, details.TimeInForce);
        Assert.Equal(TestOrders.ApprovedAt, details.ApprovedAt);

        var fill = Assert.Single(details.Fills);
        Assert.Equal(order.Fills[0].ExecutionId, fill.ExecutionId);
        Assert.Equal(1, fill.Quantity);
        Assert.Equal(10m, fill.Price);
    }

    [Fact]
    public async Task Handle_OrderOfAnotherUser_ReturnsNull()
    {
        var order = await _scope.SeedAsync(TestOrders.PendingNew());

        var details = await _scope.SendAsync(new GetByIdQuery(order.Id, Guid.NewGuid()));

        Assert.Null(details);
    }

    [Fact]
    public async Task Handle_OrderDoesNotExist_ReturnsNull()
    {
        var details = await _scope.SendAsync(new GetByIdQuery(Guid.NewGuid(), Guid.NewGuid()));

        Assert.Null(details);
    }
}
