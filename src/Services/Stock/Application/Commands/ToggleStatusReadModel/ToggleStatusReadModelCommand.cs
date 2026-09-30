using MediatR;

namespace Stock.Application.Commands.ToggleStatusReadModel;

public record ToggleStatusReadModelCommand(Guid AggregateId, bool IsOpenToTrade, long StatusVersion) : IRequest;
