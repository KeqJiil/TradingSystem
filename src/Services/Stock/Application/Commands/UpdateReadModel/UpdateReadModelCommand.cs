using MediatR;

namespace Stock.Application.Commands.UpdateReadModel;

public record UpdateReadModelCommand(Guid AggregateId, decimal PriceChange, long Version) : IRequest;