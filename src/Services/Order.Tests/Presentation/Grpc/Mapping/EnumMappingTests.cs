using Order.Domain.Aggregates;
using Order.Presentation.Grpc.Mapping;
using Xunit;
using Proto = TradingSystem.Contracts.ProtobufClasses.Order;

namespace Order.Tests.Presentation.Grpc.Mapping;

public class EnumMappingTests
{
    [Fact]
    public void ToProto_EverySymbol_RoundTripsToDomain()
    {
        foreach (var value in Enum.GetValues<Symbols>())
            Assert.Equal(value, value.ToProto().ToDomain());
    }

    [Fact]
    public void ToProto_EveryOrderSide_RoundTripsToDomain()
    {
        foreach (var value in Enum.GetValues<OrderSide>())
            Assert.Equal(value, value.ToProto().ToDomain());
    }

    [Fact]
    public void ToProto_EveryTimeInForce_RoundTripsToDomain()
    {
        foreach (var value in Enum.GetValues<TimeInForce>())
            Assert.Equal(value, value.ToProto().ToDomain());
    }

    [Fact]
    public void ToProto_EveryOrderType_RoundTripsToDomain()
    {
        foreach (var value in Enum.GetValues<OrderType>())
            Assert.Equal(value, value.ToProto().ToDomain());
    }

    [Fact]
    public void ToProto_EveryOrderStatus_RoundTripsToDomain()
    {
        foreach (var value in Enum.GetValues<OrderStatus>())
            Assert.Equal(value, value.ToProto().ToDomain());
    }

    [Fact]
    public void ToDomain_Unspecified_ReturnsNull()
    {
        Assert.Null(Proto.Symbol.Unspecified.ToDomain());
        Assert.Null(Proto.OrderSide.Unspecified.ToDomain());
        Assert.Null(Proto.TimeInForce.Unspecified.ToDomain());
        Assert.Null(Proto.OrderType.Unspecified.ToDomain());
        Assert.Null(Proto.OrderStatus.Unspecified.ToDomain());
    }

    [Fact]
    public void ToProto_UndefinedDomainValue_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ((OrderStatus)0).ToProto());
    }
}
