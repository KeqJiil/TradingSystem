using MediatR;
using Order.Application.Abstractions;
using Order.Domain;

namespace Order.Application.UseCases.RejectCancel;

public class RejectCancelCommandHandler(IOrderRepository repository, IUnitOfWork uow) : IRequestHandler<RejectCancelCommand, Result>
{
    public async Task<Result> Handle(RejectCancelCommand request, CancellationToken cancellationToken)
    {
        var order = await repository.GetByIdAsync(request.OrderId, cancellationToken);
        if (order is null)
            return Result.Failure(new Error { Type = ErrorType.NotFound, Message = $"Order with id {request.OrderId} not found" });
        
        var result = order.RejectCancel(request.Reason, request.RejectedAt);
        if (result.IsFailure)
            return result.Error;

        await uow.CommitAsync(cancellationToken);

        return Result.Success();
    }
}
