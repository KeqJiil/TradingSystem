using MediatR;
using Stock.Application.Abstractions;

namespace Stock.Application.Commands.CreateDailyReadModel;

public class CreateDailyReadModelHandler(IOutboxWriter outboxWriter) : IRequestHandler<CreateDailyReadModelCommand>
{
    public async Task Handle(CreateDailyReadModelCommand request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}