using MediatR;
using Microsoft.Extensions.Internal;
using Stock.Application.Commands.RequestDailyReadModels;

namespace Stock.Infrastructure.Cron;

public class DailyCronWorker(IServiceScopeFactory serviceScopeFactory, ISystemClock clock)
{
    public async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await using var scope = serviceScopeFactory.CreateAsyncScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        CorrelationContext.CorrelationId = Guid.NewGuid();

        await mediator.Send(new RequestDailyReadModelsCommand(clock.UtcNow.UtcDateTime.Date.AddDays(-1)),
            stoppingToken);
    }
}
