using MediatR;
using Stock.Application.Abstractions;

namespace Stock.Application.Queries.GetHourReadModel;

public record struct GetHourReadModelQuery(Guid AggregateId, DateTimeOffset DateTime) : IRequest<PriceHourReadModel?>;