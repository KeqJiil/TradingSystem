using MediatR;
using Stock.Application.Commands.CreateDailyReadModel;

namespace Stock.Infrastructure.Cron;

public class DailyCronWorker(IServiceScopeFactory serviceScopeFactory)
{
    public async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await using var scope = serviceScopeFactory.CreateAsyncScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        await mediator.Send(new CreateDailyReadModelCommand(DateTime.UtcNow.Date), stoppingToken);
    }
}