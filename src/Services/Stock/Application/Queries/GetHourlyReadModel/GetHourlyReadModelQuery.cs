using MediatR;
using Stock.Application.Abstractions;

namespace Stock.Application.Queries.GetHourlyReadModel;

public record GetHourlyReadModelQuery(
    Guid AggregateId,
    DateTimeOffset From,
    DateTimeOffset To) : IRequest<HourlyReadModel>;

public record HourlyReadModel(
    Guid AggregateId,
    IEnumerable<PriceHourReadModel> PriceChanges);