using MediatR;
using Order.Application.Abstractions;
using Order.Domain;

namespace Order.Application.UseCases.RenameStock;

public class RenameStockCommandHandler(IStockRepository repository, IUnitOfWork uow)
    : IRequestHandler<RenameStockCommand, Result>
{
    public async Task<Result> Handle(RenameStockCommand request, CancellationToken cancellationToken)
    {
        var stock = await repository.GetByIdAsync(request.StockId, cancellationToken);
        if (stock is null)
            return Result.Failure(new Error
                { Type = ErrorType.NotFound, Message = $"Stock with id {request.StockId} not found" });

        var result = stock.Rename(request.Name, request.Version);
        if (result.IsFailure)
            return result.Error;

        await uow.CommitAsync(cancellationToken);

        return Result.Success();
    }
}
