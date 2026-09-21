using FluentValidation;

namespace Stock.Application.Commands.ToggleStatusReadModel;

public class ToggleStatusReadModelValidator : AbstractValidator<ToggleStatusReadModelCommand>
{
    public ToggleStatusReadModelValidator()
    {
        RuleFor(command => command.AggregateId).NotEmpty();
        RuleFor(command => command.StatusVersion).GreaterThan(0);
    }
}
