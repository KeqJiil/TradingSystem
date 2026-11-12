using System.Globalization;
using Google.Protobuf.WellKnownTypes;
using Order.Application.Abstractions;
using Proto = TradingSystem.Contracts.ProtobufClasses.Order;

namespace Order.Presentation.Grpc.Mapping;

public static class ResponseMapping
{
    public static Proto.GetOrderResponse ToResponse(this OrderDetailsDto details)
    {
        var response = new Proto.GetOrderResponse
        {
            Order = details.Order.ToProto(),
            TimeInForce = details.TimeInForce.ToProto(),
            ApprovedAt = details.ApprovedAt.ToTimestamp(),
            ClosedAt = details.ClosedAt.ToTimestamp(),
            Fills = { details.Fills.Select(ToProto) }
        };

        if (details.CloseReason is not null)
            response.CloseReason = details.CloseReason;

        return response;
    }

    public static Proto.GetOrdersByUserIdResponse ToResponse(this IReadOnlyList<OrderDto> orders)
    {
        return new Proto.GetOrdersByUserIdResponse { Orders = { orders.Select(ToProto) } };
    }

    private static Proto.OrderDto ToProto(this OrderDto order)
    {
        var dto = new Proto.OrderDto
        {
            Id = order.Id.ToString(),
            Symbol = order.Symbol.ToProto(),
            Side = order.Side.ToProto(),
            Type = order.Type.ToProto(),
            Quantity = order.Quantity,
            FilledQuantity = order.FilledQuantity,
            Status = order.Status.ToProto(),
            CreatedAt = Timestamp.FromDateTimeOffset(order.CreatedAt)
        };

        if (order.LimitPrice is not null)
            dto.LimitPrice = order.LimitPrice.Value.ToString(CultureInfo.InvariantCulture);

        return dto;
    }

    private static Proto.OrderFillDto ToProto(FillDto fill)
    {
        return new Proto.OrderFillDto
        {
            ExecutionId = fill.ExecutionId.ToString(),
            Quantity = fill.Quantity,
            Price = fill.Price.ToString(CultureInfo.InvariantCulture),
            FilledAt = Timestamp.FromDateTimeOffset(fill.FilledAt)
        };
    }

    private static Timestamp? ToTimestamp(this DateTimeOffset? value) =>
        value is null ? null : Timestamp.FromDateTimeOffset(value.Value);
}
