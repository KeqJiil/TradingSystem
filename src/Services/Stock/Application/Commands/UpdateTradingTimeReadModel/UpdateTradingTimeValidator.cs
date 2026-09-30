using FluentValidation;

namespace Stock.Application.Commands.UpdateTradingTimeReadModel;

public class UpdateTradingTimeValidator : AbstractValidator<UpdateTradingTimeCommand>
{
    public UpdateTradingTimeValidator()
    {
        RuleFor(command => command.AggregateId).NotEmpty();

        RuleFor(command => command.TradingCloseTime)
            .GreaterThan(command => command.TradingStartTime)
            .WithMessage("'Trading Close Time' must be later than 'Trading Start Time'.");

        RuleFor(command => command.Version).GreaterThan(0);
    }
}
