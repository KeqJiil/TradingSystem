using MediatR;
using Order.Application.Abstractions;

namespace Order.Application.UseCases.GetById;

public record GetByIdQuery(Guid OrderId, Guid UserId) : IRequest<OrderDetailsDto?>;