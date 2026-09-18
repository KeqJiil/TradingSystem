using MediatR;
using Microsoft.Extensions.Internal;
using Stock.Application.Commands.RequestHourlyReadModel;

namespace Stock.Infrastructure.Cron;

public class HourlyCronWorker(IServiceScopeFactory serviceScopeFactory, ISystemClock clock)
{
    public async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await using var scope = serviceScopeFactory.CreateAsyncScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        await mediator.Send(
            new RequestHourlyReadModelCommand(DateOnly.FromDateTime(clock.UtcNow.DateTime), (byte)clock.UtcNow.Hour),
            stoppingToken);
    }
}