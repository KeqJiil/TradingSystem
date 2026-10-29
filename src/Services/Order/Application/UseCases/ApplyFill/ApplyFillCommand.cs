using MediatR;
using Order.Domain;

namespace Order.Application.UseCases.ApplyFill;

public record ApplyFillCommand(Guid OrderId, Guid ExecutionId, int Quantity, decimal Price, DateTimeOffset FilledAt) : IRequest<Result>;
