using FluentValidation;

namespace Stock.Application.Commands.ReplayReadModel;

public class ReplayReadModelValidator : AbstractValidator<ReplayReadModelCommand>
{
    public ReplayReadModelValidator()
    {
        RuleFor(command => command.AggregateId).NotEmpty();

        RuleFor(command => command.MaxVersion).GreaterThanOrEqualTo(0);
    }
}
