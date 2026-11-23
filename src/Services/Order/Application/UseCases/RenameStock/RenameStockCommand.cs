using MediatR;
using Order.Domain;

namespace Order.Application.UseCases.RenameStock;

public record RenameStockCommand(Guid StockId, string Name, long Version) : IRequest<Result>;
