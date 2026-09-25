using MediatR;

namespace Stock.Application.Commands.SetStockOpenToTrade;

public record SetStockOpenToTradeCommand(Guid Id, bool IsOpenToTrade) : IRequest<bool>;
