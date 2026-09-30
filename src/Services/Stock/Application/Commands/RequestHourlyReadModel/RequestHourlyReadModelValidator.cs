using FluentValidation;

namespace Stock.Application.Commands.RequestHourlyReadModel;

public class RequestHourlyReadModelValidator : AbstractValidator<RequestHourlyReadModelCommand>
{
    public RequestHourlyReadModelValidator()
    {
        RuleFor(command => command.Date).NotEqual(default(DateOnly));

        RuleFor(command => command.Hour).LessThanOrEqualTo((byte)23);
    }
}
