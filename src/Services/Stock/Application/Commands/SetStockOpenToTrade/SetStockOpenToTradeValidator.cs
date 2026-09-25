using FluentValidation;

namespace Stock.Application.Commands.SetStockOpenToTrade;

public class SetStockOpenToTradeValidator : AbstractValidator<SetStockOpenToTradeCommand>
{
    public SetStockOpenToTradeValidator()
    {
        RuleFor(command => command.Id).NotEmpty();
    }
}
