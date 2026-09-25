using FluentValidation;

namespace Stock.Application.Queries.GetHourlyReadModel;

public class GetHourlyReadModelValidator : AbstractValidator<GetHourlyReadModelQuery>
{
    public static readonly TimeSpan MaxRange = TimeSpan.FromDays(7);

    public GetHourlyReadModelValidator()
    {
        RuleFor(query => query.AggregateId).NotEmpty();

        RuleFor(query => query.To)
            .GreaterThan(query => query.From)
            .WithMessage("'To' must be later than 'From'.");

        RuleFor(query => query)
            .Must(query => query.To - query.From <= MaxRange)
            .OverridePropertyName("To")
            .WithMessage($"The range between 'From' and 'To' must not exceed {MaxRange.TotalDays} days.");
    }
}
