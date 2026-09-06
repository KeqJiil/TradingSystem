using MediatR;

namespace Stock.Application.Commands.ToggleStockOpenToTrade;

public record ToggleStockOpenToTradeCommand(Guid Id) : IRequest;
