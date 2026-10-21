using MediatR;
using Stock.Application.Abstractions;
using Stock.Application.Events;

namespace Stock.Application.Commands.RequestDailyReadModels;

public class RequestDailyReadModelsHandler(IPublisher publisher, IStockDataReader stockDataReader)
    : IRequestHandler<RequestDailyReadModelsCommand>
{
    public async Task Handle(RequestDailyReadModelsCommand request, CancellationToken cancellationToken)
    {
        const int pageSize = 100;
        var date = DateOnly.FromDateTime(request.Date);

        await foreach (var stockId in stockDataReader.GetAllIdsAsync(pageSize, cancellationToken))
            await publisher.Publish(new DailyReadModelRequested(stockId, date), cancellationToken);
    }
}
