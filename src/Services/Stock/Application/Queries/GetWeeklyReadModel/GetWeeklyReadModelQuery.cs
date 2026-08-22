using MediatR;
using Stock.Application.Abstractions;

namespace Stock.Application.Queries.GetWeeklyReadModel;

public record struct GetWeeklyReadModelQuery(
    Guid AggregateId,
    DateOnly StartDate,
    DateOnly EndDate) : IRequest<WeeklyReadModel>;

public readonly record struct WeeklyReadModel(
    Guid AggregateId,
    List<PriceHistoryDateOnlyReadModel> DailyReadModels
    );