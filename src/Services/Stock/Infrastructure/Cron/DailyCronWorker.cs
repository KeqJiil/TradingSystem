using MediatR;
using Stock.Application.Commands.CreateDailyReadModel;

namespace Stock.Infrastructure.Cron;

public class DailyCronWorker(IServiceScopeFactory serviceScopeFactory)
{
    public async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var scope = serviceScopeFactory.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        await mediator.Send(new CreateDailyReadModelCommand(DateTime.UtcNow.Date), stoppingToken);
    }
}