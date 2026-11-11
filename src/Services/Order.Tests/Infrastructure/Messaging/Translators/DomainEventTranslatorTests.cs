using Google.Protobuf;
using Order.Domain.Aggregates;
using Order.Domain.Events;
using Order.Domain.VO;
using TradingSystem.Contracts.ProtobufClasses.Order;
using Order.Infrastructure.Messaging.Translators;
using Order.Tests.Domain.Aggregates;
using Xunit;

namespace Order.Tests.Infrastructure.Messaging.Translators;

public class DomainEventTranslatorTests
{
    private readonly DomainEventTranslator _translator = new();
    private readonly Guid _orderId = Guid.NewGuid();

    [Fact]
    public void Translate_OrderPlaced_MapsAllFieldsIncludingLimitPrice()
    {
        var userId = Guid.NewGuid();
        var terms = OrderTerms.Create(TimeInForce.Day, OrderType.Limit, Price.Create(12.34m).Value).Value;
        var domainEvent = new OrderPlaced(_orderId, userId, Symbols.Test, OrderSide.Sell, Quantity.Create(5).Value,
            terms, TestOrders.Now);

        var translated = Assert.IsType<OrderPlacedEvent>(TranslateMessage(domainEvent));

        Assert.Equal(_orderId.ToString(), translated.AggregateId);
        Assert.Equal(userId.ToString(), translated.UserId);
        Assert.Equal(nameof(Symbols.Test), translated.Symbol);
        Assert.Equal(nameof(OrderSide.Sell), translated.Side);
        Assert.Equal(5, translated.Quantity);
        Assert.Equal(nameof(TimeInForce.Day), translated.TimeInForce);
        Assert.Equal(nameof(OrderType.Limit), translated.Type);
        Assert.Equal("12.34", translated.LimitPrice);
        Assert.Equal(TestOrders.Now, translated.OccurredAt.ToDateTimeOffset());
    }

    [Fact]
    public void Translate_MarketOrderPlaced_MapsNullLimitPrice()
    {
        var terms = OrderTerms.Create(TimeInForce.Ioc, OrderType.Market).Value;
        var domainEvent = new OrderPlaced(_orderId, Guid.NewGuid(), Symbols.Test, OrderSide.Buy,
            Quantity.Create(1).Value, terms, TestOrders.Now);

        var translated = Assert.IsType<OrderPlacedEvent>(TranslateMessage(domainEvent));

        Assert.False(translated.HasLimitPrice);
    }

    [Fact]
    public void Translate_OrderCancelRequested_MapsOrderIdAndOccurredOn()
    {
        var translated = Assert.IsType<OrderCancelRequestedEvent>(
            TranslateMessage(new OrderCancelRequested(_orderId, TestOrders.Now)));

        Assert.Equal(_orderId.ToString(), translated.AggregateId);
        Assert.Equal(TestOrders.Now, translated.OccurredAt.ToDateTimeOffset());
    }

    [Fact]
    public void Translate_OrderCanceled_MapsReasonAndQuantities()
    {
        var translated = Assert.IsType<OrderCanceledEvent>(
            TranslateMessage(new OrderCanceled(_orderId, TestOrders.Reason, 3, 7, TestOrders.Now)));

        Assert.Equal(_orderId.ToString(), translated.AggregateId);
        Assert.Equal(TestOrders.Reason, translated.Reason);
        Assert.Equal(3, translated.FilledQuantity);
        Assert.Equal(7, translated.CanceledQuantity);
        Assert.Equal(TestOrders.Now, translated.OccurredAt.ToDateTimeOffset());
    }

    [Fact]
    public void Translate_OrderFilled_MapsFilledQuantity()
    {
        var translated = Assert.IsType<OrderFilledEvent>(
            TranslateMessage(new OrderFilled(_orderId, TestOrders.Quantity, TestOrders.Now)));

        Assert.Equal(_orderId.ToString(), translated.AggregateId);
        Assert.Equal(TestOrders.Quantity, translated.Quantity);
        Assert.Equal(TestOrders.Now, translated.OccurredAt.ToDateTimeOffset());
    }

    [Fact]
    public void Translate_OrderExpired_MapsQuantities()
    {
        var translated = Assert.IsType<OrderExpiredEvent>(
            TranslateMessage(new OrderExpired(_orderId, 3, 7, TestOrders.Now)));

        Assert.Equal(_orderId.ToString(), translated.AggregateId);
        Assert.Equal(3, translated.FilledQuantity);
        Assert.Equal(7, translated.ExpiredQuantity);
        Assert.Equal(TestOrders.Now, translated.OccurredAt.ToDateTimeOffset());
    }

    [Fact]
    public void Translate_OrderRejected_MapsReason()
    {
        var translated = Assert.IsType<OrderRejectedEvent>(
            TranslateMessage(new OrderRejected(_orderId, TestOrders.Reason, TestOrders.Now)));

        Assert.Equal(_orderId.ToString(), translated.AggregateId);
        Assert.Equal(TestOrders.Reason, translated.Reason);
        Assert.Equal(TestOrders.Now, translated.OccurredAt.ToDateTimeOffset());
    }

    [Fact]
    public void Translate_OrderApproved_MapsOrderIdAndOccurredOn()
    {
        var translated = Assert.IsType<OrderApprovedEvent>(
            TranslateMessage(new OrderApproved(_orderId, TestOrders.Now)));

        Assert.Equal(_orderId.ToString(), translated.AggregateId);
        Assert.Equal(TestOrders.Now, translated.OccurredAt.ToDateTimeOffset());
    }

    [Fact]
    public void Translate_OrderCancelRejected_MapsReason()
    {
        var translated = Assert.IsType<OrderCancelRejectedEvent>(
            TranslateMessage(new OrderCancelRejected(_orderId, TestOrders.Reason, TestOrders.Now)));

        Assert.Equal(_orderId.ToString(), translated.AggregateId);
        Assert.Equal(TestOrders.Reason, translated.Reason);
        Assert.Equal(TestOrders.Now, translated.OccurredAt.ToDateTimeOffset());
    }

    [Fact]
    public void Translate_FillRecorded_MapsExecutionQuantityAndPrice()
    {
        var executionId = Guid.NewGuid();
        var domainEvent = new FillRecorded(_orderId, executionId, Quantity.Create(3).Value, Price.Create(12.5m).Value,
            TestOrders.Now);

        var translated = Assert.IsType<OrderFillRecordedEvent>(TranslateMessage(domainEvent));

        Assert.Equal(_orderId.ToString(), translated.AggregateId);
        Assert.Equal(executionId.ToString(), translated.ExecutionId);
        Assert.Equal(3, translated.Quantity);
        Assert.Equal("12.5", translated.Price);
        Assert.Equal(TestOrders.Now, translated.OccurredAt.ToDateTimeOffset());
    }

    private IMessage? TranslateMessage(DomainEvent domainEvent) => _translator.Translate(domainEvent)?.Message;
}
