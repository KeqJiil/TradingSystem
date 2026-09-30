using FluentValidation;

namespace Stock.Application.Commands.CreateReadModel;

public class CreateReadModelValidator : AbstractValidator<CreateReadModelCommand>
{
    public CreateReadModelValidator()
    {
        RuleFor(command => command.AggregateId).NotEmpty();

        RuleFor(command => command.Name)
            .NotEmpty()
            .MaximumLength(StockFieldLimits.NameMaxLength);

        RuleFor(command => command.Currency)
            .NotEmpty()
            .Length(StockFieldLimits.CurrencyLength);
    }
}
