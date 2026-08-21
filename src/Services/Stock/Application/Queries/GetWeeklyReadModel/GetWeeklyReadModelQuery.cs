using MediatR;
using Stock.Application.Abstractions;

namespace Stock.Application.Queries.GetWeeklyReadModel;

public record GetWeeklyReadModelQuery(
    Guid AggregateId,
    DateOnly StartDate,
    DateOnly EndDate) : IRequest<WeeklyReadModel>;

public record WeeklyReadModel(
    Guid AggregateId,
    List<PriceHistoryDateOnlyReadModel> DailyReadModels
    );