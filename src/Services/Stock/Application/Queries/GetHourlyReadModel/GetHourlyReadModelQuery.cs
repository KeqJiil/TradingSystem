using MediatR;

namespace Stock.Application.Queries.GetHourlyReadModel;

public record GetHourlyReadModelQuery(
    Guid AggregateId,
    DateOnly Day,
    TimeOnly Time) : IRequest<HourlyReadModel>;

public record HourlyReadModel(
    Guid AggregateId,
    DateOnly Day,
    List<PriceChange> PriceChanges);

public record PriceChange(DateTimeOffset Timestamp, decimal PriceDifference);