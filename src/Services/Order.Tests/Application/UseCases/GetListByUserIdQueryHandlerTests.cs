using System.Data.SqlTypes;
using Order.Application.UseCases.GetListByUserId;
using Order.Domain;
using Order.Domain.Aggregates;
using Order.Domain.VO;
using Order.Tests.Domain.Aggregates;
using Order.Tests.Infrastructure;
using OrderAggregate = Order.Domain.Aggregates.Order;
using Xunit;

namespace Order.Tests.Application.UseCases;

public class GetListByUserIdQueryHandlerTests(MssqlFixture fixture) : IClassFixture<MssqlFixture>, IAsyncDisposable
{
    private readonly OrderTestScope _scope = OrderTestScope.Create(fixture);

    public ValueTask DisposeAsync() => _scope.DisposeAsync();

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public async Task Handle_LimitOutOfRange_ReturnsValidation(int limit)
    {
        var result = await _scope.SendAsync(new GetListByUserIdQuery(Guid.NewGuid(), null, limit, null, null));

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
    }

    [Fact]
    public async Task Handle_UndefinedStatus_ReturnsValidation()
    {
        var result = await _scope.SendAsync(new GetListByUserIdQuery(Guid.NewGuid(), (OrderStatus)999, 10, null, null));

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Handle_OnlyOneCursorPartGiven_ReturnsValidation(bool withCreatedAt)
    {
        var query = withCreatedAt
            ? new GetListByUserIdQuery(Guid.NewGuid(), null, 10, TestOrders.Now, null)
            : new GetListByUserIdQuery(Guid.NewGuid(), null, 10, null, Guid.NewGuid());

        var result = await _scope.SendAsync(query);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
    }

    [Fact]
    public async Task Handle_NoFilter_ReturnsOnlyUsersOrdersNewestFirst()
    {
        var userId = Guid.NewGuid();
        var oldest = await _scope.SeedAsync(CreateOrder(userId, TestOrders.CreatedAt));
        var newest = await _scope.SeedAsync(CreateOrder(userId, TestOrders.CreatedAt.AddMinutes(2)));
        var middle = await _scope.SeedAsync(CreateOrder(userId, TestOrders.CreatedAt.AddMinutes(1)));
        await _scope.SeedAsync(CreateOrder(Guid.NewGuid(), TestOrders.CreatedAt.AddMinutes(3)));

        var result = await _scope.SendAsync(new GetListByUserIdQuery(userId, null, 10, null, null));

        Assert.True(result.IsSuccess);
        Assert.Equal([newest.Id, middle.Id, oldest.Id], result.Value.Select(o => o.Id));
    }

    [Fact]
    public async Task Handle_StatusFilter_ReturnsOnlyOrdersInThatStatus()
    {
        var userId = Guid.NewGuid();
        var approved = CreateOrder(userId, TestOrders.CreatedAt);
        approved.Approve(TestOrders.ApprovedAt);
        await _scope.SeedAsync(approved);
        await _scope.SeedAsync(CreateOrder(userId, TestOrders.CreatedAt.AddMinutes(1)));

        var result = await _scope.SendAsync(new GetListByUserIdQuery(userId, OrderStatus.New, 10, null, null));

        Assert.True(result.IsSuccess);
        var order = Assert.Single(result.Value);
        Assert.Equal(approved.Id, order.Id);
    }

    [Fact]
    public async Task Handle_LimitSmallerThanCount_ReturnsFirstLimitOrders()
    {
        var userId = Guid.NewGuid();
        await _scope.SeedAsync(CreateOrder(userId, TestOrders.CreatedAt));
        var middle = await _scope.SeedAsync(CreateOrder(userId, TestOrders.CreatedAt.AddMinutes(1)));
        var newest = await _scope.SeedAsync(CreateOrder(userId, TestOrders.CreatedAt.AddMinutes(2)));

        var result = await _scope.SendAsync(new GetListByUserIdQuery(userId, null, 2, null, null));

        Assert.True(result.IsSuccess);
        Assert.Equal([newest.Id, middle.Id], result.Value.Select(o => o.Id));
    }

    [Fact]
    public async Task Handle_CursorFromPreviousPage_ReturnsNextPageWithoutOverlap()
    {
        var userId = Guid.NewGuid();
        var oldest = await _scope.SeedAsync(CreateOrder(userId, TestOrders.CreatedAt));
        await _scope.SeedAsync(CreateOrder(userId, TestOrders.CreatedAt.AddMinutes(1)));
        await _scope.SeedAsync(CreateOrder(userId, TestOrders.CreatedAt.AddMinutes(2)));

        var firstPage = await _scope.SendAsync(new GetListByUserIdQuery(userId, null, 2, null, null));
        var last = firstPage.Value[^1];

        var secondPage = await _scope.SendAsync(new GetListByUserIdQuery(userId, null, 2, last.CreatedAt, last.Id));

        Assert.True(secondPage.IsSuccess);
        var order = Assert.Single(secondPage.Value);
        Assert.Equal(oldest.Id, order.Id);
    }

    [Fact]
    public async Task Handle_SameCreatedAt_OrdersByIdDescendingAcrossPages()
    {
        var userId = Guid.NewGuid();
        var ids = new List<Guid>();
        for (var i = 0; i < 3; i++)
            ids.Add((await _scope.SeedAsync(CreateOrder(userId, TestOrders.CreatedAt))).Id);

        var pages = new List<Guid>();
        DateTimeOffset? afterCreatedAt = null;
        Guid? afterId = null;
        for (var i = 0; i < 3; i++)
        {
            var page = await _scope.SendAsync(new GetListByUserIdQuery(userId, null, 1, afterCreatedAt, afterId));
            var order = Assert.Single(page.Value);
            pages.Add(order.Id);
            afterCreatedAt = order.CreatedAt;
            afterId = order.Id;
        }

        Assert.Equal(ids.OrderByDescending(id => new SqlGuid(id)), pages);
    }

    private static OrderAggregate CreateOrder(Guid userId, DateTimeOffset createdAt)
    {
        return OrderAggregate.Create(userId, Symbols.Test, Quantity.Create(1).Value,
            OrderTerms.Create(TimeInForce.Gtc, OrderType.Limit, Price.Create(100).Value).Value,
            OrderSide.Buy, createdAt).Value;
    }
}
