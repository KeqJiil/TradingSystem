using MediatR;
using Order.Application.Abstractions;
using Order.Domain;

namespace Order.Application.UseCases.SetStockStatus;

public class SetStockStatusCommandHandler(IStockRepository repository, IUnitOfWork uow)
    : IRequestHandler<SetStockStatusCommand, Result>
{
    public async Task<Result> Handle(SetStockStatusCommand request, CancellationToken cancellationToken)
    {
        var stock = await repository.GetByIdAsync(request.StockId, cancellationToken);
        if (stock is null)
            return Result.Failure(new Error
                { Type = ErrorType.NotFound, Message = $"Stock with id {request.StockId} not found" });

        var result = stock.SetStatus(request.IsOpenToTrade, request.Version);
        if (result.IsFailure)
            return result.Error;

        await uow.CommitAsync(cancellationToken);

        return Result.Success();
    }
}
