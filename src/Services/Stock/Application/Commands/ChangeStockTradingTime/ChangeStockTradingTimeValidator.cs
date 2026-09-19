using FluentValidation;

namespace Stock.Application.Commands.ChangeStockTradingTime;

public class ChangeStockTradingTimeValidator : AbstractValidator<ChangeStockTradingTimeCommand>
{
    public ChangeStockTradingTimeValidator()
    {
        RuleFor(command => command.Id).NotEmpty();

        RuleFor(command => command.CloseTime)
            .GreaterThan(command => command.OpenTime)
            .WithMessage("'Close Time' must be later than 'Open Time'.");
    }
}
