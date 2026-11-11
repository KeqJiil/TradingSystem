using Order.Domain.Aggregates;
using Proto = TradingSystem.Contracts.ProtobufClasses.Order;

namespace Order.Presentation.Grpc.Mapping;

public static class EnumMapping
{
    public static Symbols? ToDomain(this Proto.Symbol symbol) => symbol switch
    {
        Proto.Symbol.Test => Symbols.Test,
        _ => null
    };

    public static OrderSide? ToDomain(this Proto.OrderSide side) => side switch
    {
        Proto.OrderSide.Buy => OrderSide.Buy,
        Proto.OrderSide.Sell => OrderSide.Sell,
        _ => null
    };

    public static TimeInForce? ToDomain(this Proto.TimeInForce timeInForce) => timeInForce switch
    {
        Proto.TimeInForce.Day => TimeInForce.Day,
        Proto.TimeInForce.Gtc => TimeInForce.Gtc,
        Proto.TimeInForce.Ioc => TimeInForce.Ioc,
        Proto.TimeInForce.Fok => TimeInForce.Fok,
        _ => null
    };

    public static OrderType? ToDomain(this Proto.OrderType type) => type switch
    {
        Proto.OrderType.Market => OrderType.Market,
        Proto.OrderType.Limit => OrderType.Limit,
        _ => null
    };

    public static OrderStatus? ToDomain(this Proto.OrderStatus status) => status switch
    {
        Proto.OrderStatus.PendingNew => OrderStatus.PendingNew,
        Proto.OrderStatus.New => OrderStatus.New,
        Proto.OrderStatus.Rejected => OrderStatus.Rejected,
        Proto.OrderStatus.PartiallyFilled => OrderStatus.PartiallyFilled,
        Proto.OrderStatus.Filled => OrderStatus.Filled,
        Proto.OrderStatus.PendingCancel => OrderStatus.PendingCancel,
        Proto.OrderStatus.Canceled => OrderStatus.Canceled,
        Proto.OrderStatus.Expired => OrderStatus.Expired,
        _ => null
    };

    public static Proto.Symbol ToProto(this Symbols symbol) => symbol switch
    {
        Symbols.Test => Proto.Symbol.Test,
        _ => throw new ArgumentOutOfRangeException(nameof(symbol), symbol, null)
    };

    public static Proto.OrderSide ToProto(this OrderSide side) => side switch
    {
        OrderSide.Buy => Proto.OrderSide.Buy,
        OrderSide.Sell => Proto.OrderSide.Sell,
        _ => throw new ArgumentOutOfRangeException(nameof(side), side, null)
    };

    public static Proto.TimeInForce ToProto(this TimeInForce timeInForce) => timeInForce switch
    {
        TimeInForce.Day => Proto.TimeInForce.Day,
        TimeInForce.Gtc => Proto.TimeInForce.Gtc,
        TimeInForce.Ioc => Proto.TimeInForce.Ioc,
        TimeInForce.Fok => Proto.TimeInForce.Fok,
        _ => throw new ArgumentOutOfRangeException(nameof(timeInForce), timeInForce, null)
    };

    public static Proto.OrderType ToProto(this OrderType type) => type switch
    {
        OrderType.Market => Proto.OrderType.Market,
        OrderType.Limit => Proto.OrderType.Limit,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
    };

    public static Proto.OrderStatus ToProto(this OrderStatus status) => status switch
    {
        OrderStatus.PendingNew => Proto.OrderStatus.PendingNew,
        OrderStatus.New => Proto.OrderStatus.New,
        OrderStatus.Rejected => Proto.OrderStatus.Rejected,
        OrderStatus.PartiallyFilled => Proto.OrderStatus.PartiallyFilled,
        OrderStatus.Filled => Proto.OrderStatus.Filled,
        OrderStatus.PendingCancel => Proto.OrderStatus.PendingCancel,
        OrderStatus.Canceled => Proto.OrderStatus.Canceled,
        OrderStatus.Expired => Proto.OrderStatus.Expired,
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
    };
}
