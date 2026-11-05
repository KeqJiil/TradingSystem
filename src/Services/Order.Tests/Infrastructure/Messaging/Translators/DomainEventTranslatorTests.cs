using Order.Domain.Aggregates;
using Order.Domain.Events;
using Order.Domain.VO;
using Order.Infrastructure.Messaging.Events;
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

        var translated = Assert.IsType<OrderPlacedEvent>(_translator.Translate(domainEvent));

        Assert.Equal(_orderId, translated.AggregateId);
        Assert.Equal(userId, translated.UserId);
        Assert.Equal(nameof(Symbols.Test), translated.Symbol);
        Assert.Equal(nameof(OrderSide.Sell), translated.Side);
        Assert.Equal(5, translated.Quantity);
        Assert.Equal(nameof(TimeInForce.Day), translated.TimeInForce);
        Assert.Equal(nameof(OrderType.Limit), translated.Type);
        Assert.Equal(12.34m, translated.LimitPrice);
        Assert.Equal(TestOrders.Now, translated.OccurredAt);
    }

    [Fact]
    public void Translate_MarketOrderPlaced_MapsNullLimitPrice()
    {
        var terms = OrderTerms.Create(TimeInForce.Ioc, OrderType.Market).Value;
        var domainEvent = new OrderPlaced(_orderId, Guid.NewGuid(), Symbols.Test, OrderSide.Buy,
            Quantity.Create(1).Value, terms, TestOrders.Now);

        var translated = Assert.IsType<OrderPlacedEvent>(_translator.Translate(domainEvent));

        Assert.Null(translated.LimitPrice);
    }

    [Fact]
    public void Translate_OrderCancelRequested_MapsOrderIdAndOccurredOn()
    {
        var translated = Assert.IsType<OrderCancelRequestedEvent>(
            _translator.Translate(new OrderCancelRequested(_orderId, TestOrders.Now)));

        Assert.Equal(_orderId, translated.AggregateId);
        Assert.Equal(TestOrders.Now, translated.OccurredAt);
    }

    [Fact]
    public void Translate_OrderCanceled_MapsReason()
    {
        var translated = Assert.IsType<OrderCanceledEvent>(
            _translator.Translate(new OrderCanceled(_orderId, TestOrders.Reason, TestOrders.Now)));

        Assert.Equal(_orderId, translated.AggregateId);
        Assert.Equal(TestOrders.Reason, translated.Reason);
        Assert.Equal(TestOrders.Now, translated.OccurredAt);
    }

    [Fact]
    public void Translate_OrderFilled_MapsFilledQuantity()
    {
        var translated = Assert.IsType<OrderFilledEvent>(
            _translator.Translate(new OrderFilled(_orderId, TestOrders.Quantity, TestOrders.Now)));

        Assert.Equal(_orderId, translated.AggregateId);
        Assert.Equal(TestOrders.Quantity, translated.Quantity);
        Assert.Equal(TestOrders.Now, translated.OccurredAt);
    }

    [Fact]
    public void Translate_OrderExpired_MapsOrderIdAndOccurredOn()
    {
        var translated = Assert.IsType<OrderExpiredEvent>(
            _translator.Translate(new OrderExpired(_orderId, TestOrders.Now)));

        Assert.Equal(_orderId, translated.AggregateId);
        Assert.Equal(TestOrders.Now, translated.OccurredAt);
    }

    [Fact]
    public void Translate_OrderRejected_MapsReason()
    {
        var translated = Assert.IsType<OrderRejectedEvent>(
            _translator.Translate(new OrderRejected(_orderId, TestOrders.Reason, TestOrders.Now)));

        Assert.Equal(_orderId, translated.AggregateId);
        Assert.Equal(TestOrders.Reason, translated.Reason);
        Assert.Equal(TestOrders.Now, translated.OccurredAt);
    }

    [Fact]
    public void Translate_OrderApproved_MapsOrderIdAndOccurredOn()
    {
        var translated = Assert.IsType<OrderApprovedEvent>(
            _translator.Translate(new OrderApproved(_orderId, TestOrders.Now)));

        Assert.Equal(_orderId, translated.AggregateId);
        Assert.Equal(TestOrders.Now, translated.OccurredAt);
    }

    [Fact]
    public void Translate_OrderCancelRejected_MapsReason()
    {
        var translated = Assert.IsType<OrderCancelRejectedEvent>(
            _translator.Translate(new OrderCancelRejected(_orderId, TestOrders.Reason, TestOrders.Now)));

        Assert.Equal(_orderId, translated.AggregateId);
        Assert.Equal(TestOrders.Reason, translated.Reason);
        Assert.Equal(TestOrders.Now, translated.OccurredAt);
    }

    [Fact]
    public void Translate_FillRecorded_MapsExecutionQuantityAndPrice()
    {
        var executionId = Guid.NewGuid();
        var domainEvent = new FillRecorded(_orderId, executionId, Quantity.Create(3).Value, Price.Create(12.5m).Value,
            TestOrders.Now);

        var translated = Assert.IsType<OrderFillRecordedEvent>(_translator.Translate(domainEvent));

        Assert.Equal(_orderId, translated.AggregateId);
        Assert.Equal(executionId, translated.ExecutionId);
        Assert.Equal(3, translated.Quantity);
        Assert.Equal(12.5m, translated.Price);
        Assert.Equal(TestOrders.Now, translated.OccurredAt);
    }
}
