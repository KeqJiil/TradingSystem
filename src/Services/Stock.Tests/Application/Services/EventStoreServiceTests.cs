using Moq;
using Stock.Application.Abstractions;
using Stock.Application.Events;
using Stock.Application.Services;
using Xunit;

namespace Stock.Tests.Application.Services;

public class EventStoreServiceTests
{
    private readonly Mock<IUnitOfWorkDecorator> _uowMock = new();
    private readonly Mock<IOutboxWriter> _outboxWriterMock = new();
    private readonly Mock<IStockEventStore> _stockEventStoreMock = new();

    private readonly EventStoreService _eventStoreService;

    public EventStoreServiceTests()
    {
        _eventStoreService =
            new EventStoreService(_stockEventStoreMock.Object, _uowMock.Object, _outboxWriterMock.Object);
        SetupMocks();
    }

    [Fact]
    public async Task ChangePriceAppendAsync_ShouldCallAppendAndWriteAsync()
    {
        var request = new ChangePriceRequested(Guid.NewGuid(), 10.0m, DateTimeOffset.UtcNow);

        var result = await _eventStoreService.ChangePriceAppendAsync(request, CancellationToken.None);

        _stockEventStoreMock.Verify(
            x => x.AppendAsync(
                It.Is<PriceChangedEvent>(e =>
                    e.AggregateId == request.AggregateId && e.PriceChange == request.PriceChange),
                It.IsAny<CancellationToken>()), Times.Once);
        _outboxWriterMock.Verify(
            x => x.WriteAsync(
                It.Is<PriceChangedEvent>(e =>
                    e.AggregateId == request.AggregateId && e.PriceChange == request.PriceChange), request.AggregateId,
                It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(request.AggregateId, result);
    }

    [Fact]
    public async Task ChangePriceAppendAsync_ShouldThrowException_WhenUowThrows()
    {
        var request = new ChangePriceRequested(Guid.NewGuid(), 10.0m, DateTimeOffset.UtcNow);

        _uowMock.Setup(x => x.ExecuteAsync(It.IsAny<Func<Task>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Unit of work failed"));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _eventStoreService.ChangePriceAppendAsync(request, CancellationToken.None));
    }

    private void SetupMocks()
    {
        _uowMock.Setup(x => x.ExecuteAsync(It.IsAny<Func<Task>>(), It.IsAny<CancellationToken>()))
            .Returns<Func<Task>, CancellationToken>((action, _) => action());

        _outboxWriterMock
            .Setup(x => x.WriteAsync(It.IsAny<PriceChangedEvent>(), It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _stockEventStoreMock
            .Setup(x => x.AppendAsync(It.IsAny<PriceChangedEvent>(), It.IsAny<CancellationToken>()))
            .Returns<PriceChangedEvent, CancellationToken>((evt, _) => Task.FromResult(evt with { Version = 42 }));
    }
}