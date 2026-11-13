using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Order.Presentation.Grpc.Mapping;
using Proto = TradingSystem.Contracts.ProtobufClasses.Order;

namespace Order.Presentation.Grpc;

[Authorize]
public class OrderGrpcService(IMediator mediator) : Proto.OrderService.OrderServiceBase
{
    public override async Task<Proto.CreateOrderResponse> CreateOrder(Proto.CreateOrderRequest request,
        ServerCallContext context)
    {
        var command = request.ToCommand(context.GetUserId()).GetValueOrThrow();
        var orderId = (await mediator.Send(command, context.CancellationToken)).GetValueOrThrow();

        return new Proto.CreateOrderResponse { OrderId = orderId.ToString() };
    }

    public override async Task<Empty> RequestOrderCancel(Proto.RequestOrderCancelRequest request,
        ServerCallContext context)
    {
        var command = request.ToCommand(context.GetUserId()).GetValueOrThrow();
        (await mediator.Send(command, context.CancellationToken)).ThrowIfFailure();

        return new Empty();
    }

    public override async Task<Proto.GetOrderResponse> GetById(Proto.GetOrderRequest request,
        ServerCallContext context)
    {
        var query = request.ToQuery(context.GetUserId()).GetValueOrThrow();
        var details = await mediator.Send(query, context.CancellationToken)
                      ?? throw new RpcException(new Status(StatusCode.NotFound, "Order not found."));

        return details.ToResponse();
    }

    public override async Task<Proto.GetOrdersByUserIdResponse> GetListByUserId(
        Proto.GetOrdersByUserIdRequest request, ServerCallContext context)
    {
        var query = request.ToQuery(context.GetUserId()).GetValueOrThrow();
        var orders = (await mediator.Send(query, context.CancellationToken)).GetValueOrThrow();

        return orders.ToResponse();
    }
}
