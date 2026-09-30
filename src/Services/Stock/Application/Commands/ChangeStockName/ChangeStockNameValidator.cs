using FluentValidation;

namespace Stock.Application.Commands.ChangeStockName;

public class ChangeStockNameValidator : AbstractValidator<ChangeStockNameCommand>
{
    public ChangeStockNameValidator()
    {
        RuleFor(command => command.Id).NotEmpty();

        RuleFor(command => command.Name)
            .NotEmpty()
            .MaximumLength(StockFieldLimits.NameMaxLength);
    }
}
