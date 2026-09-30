using MediatR;

namespace Stock.Application.Commands.UpdateNameReadModel;

public record UpdateNameReadModelCommand(Guid AggregateId, string Name, long Version) : IRequest;
