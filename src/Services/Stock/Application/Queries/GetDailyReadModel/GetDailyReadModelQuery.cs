using MediatR;

namespace Stock.Application.Queries.GetDailyReadModel;

public record struct GetDailyReadModelQuery(
    Guid AggregateId,
    DateOnly Date) : IRequest<DailyReadModel?>;
    
public readonly record struct DailyReadModel(
    Guid AggregateId,
    DateOnly Date,
    decimal OpenPrice,
    decimal ClosePrice,
    decimal HighPrice,
    decimal LowPrice,
    decimal PriceDifference);