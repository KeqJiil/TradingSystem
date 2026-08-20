using MediatR;
using Stock.Application.Queries.GetDailyReadModel;

namespace Stock.Application.Queries.GetWeeklyReadModel;

public record GetWeeklyReadModelQuery(
    Guid AggregateId,
    byte WeekNumber) : IRequest<WeeklyReadModel>;

public record WeeklyReadModel(
    Guid AggregateId,
    byte WeekNumber,
    DailyReadModel? Sunday,
    DailyReadModel? Monday,
    DailyReadModel? Tuesday,
    DailyReadModel? Wednesday,
    DailyReadModel? Thursday,
    DailyReadModel? Friday,
    DailyReadModel? Saturday
    );