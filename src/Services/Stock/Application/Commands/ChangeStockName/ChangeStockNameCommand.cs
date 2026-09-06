using MediatR;

namespace Stock.Application.Commands.ChangeStockName;

public record ChangeStockNameCommand(Guid Id, string Name) : IRequest;
