using FluentValidation;

namespace Stock.Application.Queries.GetWeeklyReadModel;

public class GetWeeklyReadModelValidator : AbstractValidator<GetWeeklyReadModelQuery>
{
    public const int MaxRangeDays = 366;

    public GetWeeklyReadModelValidator()
    {
        RuleFor(query => query.AggregateId).NotEmpty();

        RuleFor(query => query.EndDate)
            .GreaterThan(query => query.StartDate)
            .OverridePropertyName("To")
            .WithMessage("'To' must be later than 'From'.");

        RuleFor(query => query)
            .Must(query => query.EndDate.DayNumber - query.StartDate.DayNumber <= MaxRangeDays)
            .OverridePropertyName("To")
            .WithMessage($"The range between 'From' and 'To' must not exceed {MaxRangeDays} days.");
    }
}
