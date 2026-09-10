using MediatR;

namespace Stock.Application.Commands.ReplayReadModel;

public record ReplayReadModelCommand(
    Guid AggregateId,
    long MaxVersion
) : IRequest;