using FluentValidation;

namespace Stock.Application.Commands.RequestDailyReadModels;

public class RequestDailyReadModelsValidator : AbstractValidator<RequestDailyReadModelsCommand>
{
    public RequestDailyReadModelsValidator()
    {
        RuleFor(command => command.Date).NotEqual(default(DateTime));
    }
}
