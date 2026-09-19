using FluentValidation;

namespace Stock.Application.Commands.ChangePrice;

public class ChangePriceValidator : AbstractValidator<ChangePriceCommand>
{
    public ChangePriceValidator()
    {
        RuleFor(command => command.EventId).NotEmpty();

        RuleFor(command => command.AggregateId).NotEmpty();

        RuleFor(command => command.OccuredAt).NotEqual(default(DateTimeOffset));
    }
}
