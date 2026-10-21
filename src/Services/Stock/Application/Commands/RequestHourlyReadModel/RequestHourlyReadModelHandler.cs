using MediatR;
using Stock.Application.Abstractions;
using Stock.Application.Events;

namespace Stock.Application.Commands.RequestHourlyReadModel;

public class RequestHourlyReadModelHandler(IPublisher publisher, IStockDataReader stockDataReader)
    : IRequestHandler<RequestHourlyReadModelCommand>
{
    public async Task Handle(RequestHourlyReadModelCommand request, CancellationToken cancellationToken)
    {
        const int pageSize = 100;

        await foreach (var stockId in stockDataReader.GetAllIdsAsync(pageSize, cancellationToken))
            await publisher.Publish(new HourlyReadModelRequested(stockId, request.Date, request.Hour),
                cancellationToken);
    }
}
