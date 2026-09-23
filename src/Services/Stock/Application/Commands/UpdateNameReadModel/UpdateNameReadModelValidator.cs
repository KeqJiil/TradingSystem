using FluentValidation;

namespace Stock.Application.Commands.UpdateNameReadModel;

public class UpdateNameReadModelValidator : AbstractValidator<UpdateNameReadModelCommand>
{
    public UpdateNameReadModelValidator()
    {
        RuleFor(command => command.AggregateId).NotEmpty();

        RuleFor(command => command.Name)
            .NotEmpty()
            .MaximumLength(StockFieldLimits.NameMaxLength);

        RuleFor(command => command.Version).GreaterThan(0);
    }
}
