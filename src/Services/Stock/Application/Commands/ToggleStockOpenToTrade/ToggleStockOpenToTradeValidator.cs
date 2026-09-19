using FluentValidation;

namespace Stock.Application.Commands.ToggleStockOpenToTrade;

public class ToggleStockOpenToTradeValidator : AbstractValidator<ToggleStockOpenToTradeCommand>
{
    public ToggleStockOpenToTradeValidator()
    {
        RuleFor(command => command.Id).NotEmpty();
    }
}
