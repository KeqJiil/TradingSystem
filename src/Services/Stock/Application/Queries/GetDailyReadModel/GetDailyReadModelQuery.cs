using MediatR;

namespace Stock.Application.Queries.GetDailyReadModel;

public record GetDailyReadModelQuery(
    Guid AggregateId,
    DateOnly Date) : IRequest<DailyReadModel?>;
    
public record DailyReadModel(
    Guid AggregateId,
    DateOnly Date,
    decimal OpenPrice,
    decimal ClosePrice,
    decimal HighPrice,
    decimal LowPrice,
    decimal PriceDifference);