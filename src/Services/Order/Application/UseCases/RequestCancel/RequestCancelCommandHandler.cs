using MediatR;
using Order.Application.Abstractions;
using Order.Domain;

namespace Order.Application.UseCases.RequestCancel;

public class RequestCancelCommandHandler(IOrderRepository repository, IUnitOfWork uow, TimeProvider timeProvider)
    : IRequestHandler<RequestCancelCommand, Result>
{
    public async Task<Result> Handle(RequestCancelCommand request, CancellationToken cancellationToken)
    {
        var order = await repository.GetByIdAsync(request.OrderId, cancellationToken);
        if (order is null)
            return Result.Failure(new Error
                { Type = ErrorType.NotFound, Message = $"Order with id {request.OrderId} not found" });

        if (order.UserId != request.UserId)
            return Result.Failure(new Error
            {
                Type = ErrorType.NotFound, // This is intentional to avoid leaking information about the existence of the order
                Message = $"User with id {request.UserId} is not allowed to cancel order with id {request.OrderId}"
            });

        var result = order.RequestCancel(timeProvider.GetUtcNow());
        if (result.IsFailure)
            return result.Error;

        await uow.CommitAsync(cancellationToken);

        return Result.Success();
    }
}