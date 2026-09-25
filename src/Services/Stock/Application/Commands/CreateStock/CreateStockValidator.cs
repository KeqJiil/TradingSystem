using FluentValidation;

namespace Stock.Application.Commands.CreateStock;

public class CreateStockValidator : AbstractValidator<CreateStockCommand>
{
    public CreateStockValidator()
    {
        RuleFor(command => command.Id).NotEmpty();

        RuleFor(command => command.Name)
            .NotEmpty()
            .MaximumLength(StockFieldLimits.NameMaxLength);

        RuleFor(command => command.Currency)
            .NotEmpty()
            .Length(StockFieldLimits.CurrencyLength)
            .Matches("^[A-Z]+$")
            .WithMessage("'Currency' must be an uppercase ISO 4217 code, for example USD.");

        RuleFor(command => command.TradingEndTime)
            .GreaterThan(command => command.TradingStartTime)
            .WithMessage("'Trading End Time' must be later than 'Trading Start Time'.");
    }
}
