using System.Globalization;
using Order.Application.UseCases.GetById;
using Order.Application.UseCases.GetListByUserId;
using Order.Application.UseCases.PlaceOrder;
using Order.Application.UseCases.RequestCancel;
using Order.Domain;
using Order.Domain.Aggregates;
using Proto = TradingSystem.Contracts.ProtobufClasses.Order;

namespace Order.Presentation.Grpc.Mapping;

public static class RequestMapping
{
    public static Result<PlaceOrderCommand> ToCommand(this Proto.CreateOrderRequest request, Guid userId)
    {
        var symbol = request.Symbol.ToDomain();
        var side = request.OrderSide.ToDomain();
        var timeInForce = request.TimeInForce.ToDomain();
        var type = request.OrderType.ToDomain();
        if (symbol is null || side is null || timeInForce is null || type is null)
            return Invalid("order parameters");

        decimal? limitPrice = null;
        if (request.HasLimitPrice)
        {
            if (!decimal.TryParse(request.LimitPrice, NumberStyles.Number, CultureInfo.InvariantCulture,
                    out var price))
                return Invalid("limit price");
            limitPrice = price;
        }

        return new PlaceOrderCommand(userId, symbol.Value, request.Quantity, side.Value, timeInForce.Value,
            type.Value, limitPrice);
    }

    public static Result<RequestCancelCommand> ToCommand(this Proto.RequestOrderCancelRequest request, Guid userId)
    {
        return Guid.TryParse(request.OrderId, out var orderId)
            ? new RequestCancelCommand(orderId, userId)
            : Invalid("order id");
    }

    public static Result<GetByIdQuery> ToQuery(this Proto.GetOrderRequest request, Guid userId)
    {
        return Guid.TryParse(request.OrderId, out var orderId)
            ? new GetByIdQuery(orderId, userId)
            : Invalid("order id");
    }

    public static Result<GetListByUserIdQuery> ToQuery(this Proto.GetOrdersByUserIdRequest request, Guid userId)
    {
        OrderStatus? status = null;
        if (request.HasStatus)
        {
            status = request.Status.ToDomain();
            if (status is null)
                return Invalid("status");
        }

        Guid? afterId = null;
        if (request.HasAfterId)
        {
            if (!Guid.TryParse(request.AfterId, out var id))
                return Invalid("after id");
            afterId = id;
        }

        return new GetListByUserIdQuery(userId, status, request.Limit, request.AfterCreatedAt?.ToDateTimeOffset(),
            afterId);
    }

    private static Error Invalid(string field) =>
        new() { Type = ErrorType.Validation, Message = $"Invalid {field}." };
}
