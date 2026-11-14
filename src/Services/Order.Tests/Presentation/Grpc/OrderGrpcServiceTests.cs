using System.Security.Cryptography;
using Grpc.Core;
using Microsoft.EntityFrameworkCore;
using Order.Presentation;
using Messaging.Abstractions;
using Order.Domain.Aggregates;
using Order.Domain.VO;
using Order.Tests.Domain.Aggregates;
using Xunit;
using OrderAggregate = Order.Domain.Aggregates.Order;
using Proto = TradingSystem.Contracts.ProtobufClasses.Order;

namespace Order.Tests.Presentation.Grpc;

public class OrderGrpcServiceTests(OrderGrpcFixture fixture) : IClassFixture<OrderGrpcFixture>
{
    private readonly Proto.OrderService.OrderServiceClient _client = fixture.Client;
    private readonly Guid _userId = Guid.NewGuid();

    [Fact]
    public async Task GetListByUserId_NoToken_ReturnsUnauthenticated()
    {
        var exception = await Assert.ThrowsAsync<RpcException>(async () =>
            await _client.GetListByUserIdAsync(new Proto.GetOrdersByUserIdRequest { Limit = 10 }));

        Assert.Equal(StatusCode.Unauthenticated, exception.StatusCode);
    }

    [Fact]
    public async Task GetListByUserId_TokenSignedWithOtherKey_ReturnsUnauthenticated()
    {
        var auth = OrderGrpcFixture.AuthFor(_userId.ToString(), RandomNumberGenerator.GetBytes(32));

        var exception = await Assert.ThrowsAsync<RpcException>(async () =>
            await _client.GetListByUserIdAsync(new Proto.GetOrdersByUserIdRequest { Limit = 10 }, auth));

        Assert.Equal(StatusCode.Unauthenticated, exception.StatusCode);
    }

    [Fact]
    public async Task GetListByUserId_SubjectIsNotGuid_ReturnsUnauthenticated()
    {
        var exception = await Assert.ThrowsAsync<RpcException>(async () =>
            await _client.GetListByUserIdAsync(new Proto.GetOrdersByUserIdRequest { Limit = 10 },
                fixture.AuthFor("not-a-guid")));

        Assert.Equal(StatusCode.Unauthenticated, exception.StatusCode);
    }

    [Fact]
    public async Task CreateOrder_ValidLimitOrder_PersistsOrderForTokenUser()
    {
        var response = await _client.CreateOrderAsync(LimitOrderRequest(quantity: 10), fixture.AuthFor(_userId));

        await using var context = fixture.Database.CreateContext();
        var order = await context.Orders.AsNoTracking().SingleAsync(o => o.Id == Guid.Parse(response.OrderId));
        Assert.Equal(_userId, order.UserId);
        Assert.Equal(Symbols.Test, order.Symbol);
        Assert.Equal(10, order.Quantity.Value);
        Assert.Equal(12.5m, order.OrderTerms.LimitPrice!.Value);
        Assert.Equal(OrderStatus.PendingNew, order.Status);
    }

    [Fact]
    public async Task CreateOrder_CorrelationIdInMetadata_PublishedEventCarriesIt()
    {
        var correlationId = Guid.NewGuid().ToString();
        var headers = fixture.AuthFor(_userId);
        headers.Add(CorrelationMiddleware.Header, correlationId);

        var response = await _client.CreateOrderAsync(LimitOrderRequest(quantity: 10), headers);

        var published = Assert.Single(await fixture.Published.OfTypeAsync<Proto.OrderPlacedEvent>(response.OrderId));
        Assert.Equal(correlationId, published.Headers[MessagingHeaders.CorrelationId]);
    }

    [Fact]
    public async Task CreateOrder_CorrelationIdInMetadata_EchoedInResponseHeaders()
    {
        var correlationId = Guid.NewGuid().ToString();
        var headers = fixture.AuthFor(_userId);
        headers.Add(CorrelationMiddleware.Header, correlationId);

        using var call = _client.CreateOrderAsync(LimitOrderRequest(quantity: 10), headers);
        await call;
        var responseHeaders = await call.ResponseHeadersAsync;

        Assert.Equal(correlationId, responseHeaders.GetValue(CorrelationMiddleware.Header));
    }

    [Fact]
    public async Task CreateOrder_NoCorrelationId_GeneratedIdEchoedAndPublished()
    {
        using var call = _client.CreateOrderAsync(LimitOrderRequest(quantity: 10), fixture.AuthFor(_userId));
        var response = await call;
        var responseHeaders = await call.ResponseHeadersAsync;

        var correlationId = responseHeaders.GetValue(CorrelationMiddleware.Header);
        Assert.True(Guid.TryParse(correlationId, out _));
        var published = Assert.Single(await fixture.Published.OfTypeAsync<Proto.OrderPlacedEvent>(response.OrderId));
        Assert.Equal(correlationId, published.Headers[MessagingHeaders.CorrelationId]);
    }

    [Fact]
    public async Task CreateOrder_DomainRejectsQuantity_ReturnsInvalidArgument()
    {
        var exception = await Assert.ThrowsAsync<RpcException>(async () =>
            await _client.CreateOrderAsync(LimitOrderRequest(quantity: 0), fixture.AuthFor(_userId)));

        Assert.Equal(StatusCode.InvalidArgument, exception.StatusCode);
    }

    [Fact]
    public async Task GetById_OwnOrder_ReturnsDetails()
    {
        var order = await SeedAsync(NewLimitOrder(_userId));

        var response = await _client.GetByIdAsync(new Proto.GetOrderRequest { OrderId = order.Id.ToString() },
            fixture.AuthFor(_userId));

        Assert.Equal(order.Id.ToString(), response.Order.Id);
        Assert.Equal(Proto.OrderStatus.PendingNew, response.Order.Status);
        Assert.Equal(12.5m, decimal.Parse(response.Order.LimitPrice, System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal(Proto.TimeInForce.Day, response.TimeInForce);
    }

    [Fact]
    public async Task GetById_OrderOfAnotherUser_ReturnsNotFound()
    {
        var order = await SeedAsync(NewLimitOrder(Guid.NewGuid()));

        var exception = await Assert.ThrowsAsync<RpcException>(async () =>
            await _client.GetByIdAsync(new Proto.GetOrderRequest { OrderId = order.Id.ToString() },
                fixture.AuthFor(_userId)));

        Assert.Equal(StatusCode.NotFound, exception.StatusCode);
    }

    [Fact]
    public async Task RequestOrderCancel_OwnOrder_MovesToPendingCancel()
    {
        var order = await SeedAsync(NewLimitOrder(_userId));

        await _client.RequestOrderCancelAsync(new Proto.RequestOrderCancelRequest { OrderId = order.Id.ToString() },
            fixture.AuthFor(_userId));

        await using var context = fixture.Database.CreateContext();
        var updated = await context.Orders.AsNoTracking().SingleAsync(o => o.Id == order.Id);
        Assert.Equal(OrderStatus.PendingCancel, updated.Status);
    }

    [Fact]
    public async Task RequestOrderCancel_OrderOfAnotherUser_ReturnsNotFound()
    {
        var order = await SeedAsync(NewLimitOrder(Guid.NewGuid()));

        var exception = await Assert.ThrowsAsync<RpcException>(async () =>
            await _client.RequestOrderCancelAsync(
                new Proto.RequestOrderCancelRequest { OrderId = order.Id.ToString() }, fixture.AuthFor(_userId)));

        Assert.Equal(StatusCode.NotFound, exception.StatusCode);
    }

    [Fact]
    public async Task RequestOrderCancel_FilledOrder_ReturnsFailedPrecondition()
    {
        var order = await SeedAsync(TestOrders.InStatus(OrderStatus.Filled));

        var exception = await Assert.ThrowsAsync<RpcException>(async () =>
            await _client.RequestOrderCancelAsync(
                new Proto.RequestOrderCancelRequest { OrderId = order.Id.ToString() }, fixture.AuthFor(order.UserId)));

        Assert.Equal(StatusCode.FailedPrecondition, exception.StatusCode);
    }

    [Fact]
    public async Task GetListByUserId_OrdersOfSeveralUsers_ReturnsOnlyTokenUsersOrders()
    {
        var own = await SeedAsync(NewLimitOrder(_userId));
        await SeedAsync(NewLimitOrder(Guid.NewGuid()));

        var response = await _client.GetListByUserIdAsync(new Proto.GetOrdersByUserIdRequest { Limit = 10 },
            fixture.AuthFor(_userId));

        Assert.Equal([own.Id.ToString()], response.Orders.Select(o => o.Id));
    }

    private static Proto.CreateOrderRequest LimitOrderRequest(int quantity) => new()
    {
        Symbol = Proto.Symbol.Test,
        Quantity = quantity,
        OrderSide = Proto.OrderSide.Buy,
        TimeInForce = Proto.TimeInForce.Day,
        OrderType = Proto.OrderType.Limit,
        LimitPrice = "12.5"
    };

    private static OrderAggregate NewLimitOrder(Guid userId)
    {
        var terms = OrderTerms.Create(TimeInForce.Day, OrderType.Limit, Price.Create(12.5m).Value).Value;
        return OrderAggregate.Create(userId, Symbols.Test, Quantity.Create(10).Value, terms, OrderSide.Buy,
            TestOrders.CreatedAt).Value;
    }

    private async Task<OrderAggregate> SeedAsync(OrderAggregate order)
    {
        await using var context = fixture.Database.CreateContext();
        context.Orders.Add(order);
        await context.SaveChangesAsync();
        return order;
    }
}
