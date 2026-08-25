using MediatR;

namespace Stock.Application.Commands.ToggleStatusReadModel;

public class ToggleStatusReadModelHandler : IRequestHandler<ToggleStatusReadModelCommand>
{
    public async Task Handle(ToggleStatusReadModelCommand request, CancellationToken cancellationToken)
    {
        
    }
}