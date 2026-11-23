using MediatR;
using Order.Domain;

namespace Order.Application.UseCases.SetStockStatus;

public record SetStockStatusCommand(Guid StockId, bool IsOpenToTrade, long Version) : IRequest<Result>;
