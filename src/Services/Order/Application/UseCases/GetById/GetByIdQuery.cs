using MediatR;
using Order.Application.Abstractions;
using Order.Domain;

namespace Order.Application.UseCases.GetById;

public record GetByIdQuery(Guid OrderId, Guid UserId) : IRequest<Result<OrderDetailsDto>>;
