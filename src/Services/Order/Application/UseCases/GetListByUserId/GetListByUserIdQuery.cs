using MediatR;
using Order.Application.Abstractions;
using Order.Domain.Aggregates;

namespace Order.Application.UseCases.GetListByUserId;

public record GetListByUserIdQuery(
    Guid UserId,
    OrderStatus? Status,
    int Limit,
    DateTimeOffset? AfterCreatedAt,
    Guid? AfterId) : IRequest<IReadOnlyList<OrderDto>>;
