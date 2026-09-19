using FluentValidation;

namespace Stock.Application.Commands.UpdateReadModel;

public class UpdateReadModelValidator : AbstractValidator<UpdateReadModelCommand>
{
    public UpdateReadModelValidator()
    {
        RuleFor(command => command.AggregateId).NotEmpty();

        RuleFor(command => command.Version).GreaterThanOrEqualTo(0);
    }
}
