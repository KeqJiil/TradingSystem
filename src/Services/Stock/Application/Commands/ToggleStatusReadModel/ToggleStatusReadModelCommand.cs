using MediatR;

namespace Stock.Application.Commands.ToggleStatusReadModel;

public record ToggleStatusReadModelCommand(Guid AggregateId) : IRequest;