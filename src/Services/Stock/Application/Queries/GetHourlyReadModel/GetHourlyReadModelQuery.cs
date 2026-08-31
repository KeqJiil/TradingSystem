using MediatR;

namespace Stock.Application.Queries.GetHourlyReadModel;

public record GetHourlyReadModelQuery(
    Guid AggregateId,
    DateTimeOffset From,
    DateTimeOffset To,
    TimeOnly Time) : IRequest<HourlyReadModel?>;

public record HourlyReadModel(
    Guid AggregateId,
    IEnumerable<PriceChange> PriceChanges);

public record PriceChange(DateTimeOffset Timestamp, decimal PriceDifference);