using MediatR;
using Order.Application.Abstractions;
using Order.Domain;

namespace Order.Application.UseCases.ExpireOrder;

public class ExpireOrderCommandHandler(IOrderRepository repository, IUnitOfWork uow) : IRequestHandler<ExpireOrderCommand, Result>
{
    public async Task<Result> Handle(ExpireOrderCommand request, CancellationToken cancellationToken)
    {
        var order = await repository.GetByIdAsync(request.OrderId, cancellationToken);
        if (order is null)
            return Result.Failure(new Error
                { Type = ErrorType.NotFound, Message = $"Order with id {request.OrderId} not found" });

        var result = order.Expire(request.ExpiredAt);
        if (result.IsFailure)
            return result.Error;

        await uow.CommitAsync(cancellationToken);

        return Result.Success();
    }
}
