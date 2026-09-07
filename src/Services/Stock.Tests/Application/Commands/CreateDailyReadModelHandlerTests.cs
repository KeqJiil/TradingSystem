using System.Runtime.CompilerServices;
using Moq;
using Stock.Application.Abstractions;
using Stock.Application.Commands.CreateDailyReadModel;
using Stock.Application.Events;
using Xunit;

namespace Stock.Tests.Application.Commands;

public class CreateDailyReadModelHandlerTests
{
    private readonly Mock<IOutboxWriter> _outboxWriter = new();
    private readonly Mock<IStockDataReader> _stockDataReader = new();
    private readonly List<List<(Guid AggregateId, DailyReadModelRequested Payload)>> _writtenBatches = new();

    private CreateDailyReadModelHandler CreateHandler()
    {
        return new CreateDailyReadModelHandler(_outboxWriter.Object, _stockDataReader.Object);
    }

    [Fact]
    public async Task NoStocks_DoesNotWriteAnything()
    {
        SetUpWriterCapture();
        SetUpReader([]);

        await CreateHandler().Handle(new CreateDailyReadModelCommand(DateTime.UtcNow), CancellationToken.None);

        Assert.Empty(_writtenBatches);
    }

    [Fact]
    public async Task FewerThanBatchSize_FlushesOnceAtTheEnd()
    {
        var ids = Enumerable.Range(0, 99).Select(_ => Guid.NewGuid()).ToList();
        SetUpWriterCapture();
        SetUpReader(ids);

        await CreateHandler().Handle(new CreateDailyReadModelCommand(DateTime.UtcNow), CancellationToken.None);

        var batch = Assert.Single(_writtenBatches);
        Assert.Equal(99, batch.Count);
    }

    [Fact]
    public async Task ExactlyBatchSize_FlushesOnceInsideTheLoop_NotAgainAtTheEnd()
    {
        var ids = Enumerable.Range(0, 100).Select(_ => Guid.NewGuid()).ToList();
        SetUpWriterCapture();
        SetUpReader(ids);

        await CreateHandler().Handle(new CreateDailyReadModelCommand(DateTime.UtcNow), CancellationToken.None);

        var batch = Assert.Single(_writtenBatches);
        Assert.Equal(100, batch.Count);
    }

    [Fact]
    public async Task MoreThanBatchSize_SplitsIntoMultipleBatches()
    {
        var ids = Enumerable.Range(0, 101).Select(_ => Guid.NewGuid()).ToList();
        SetUpWriterCapture();
        SetUpReader(ids);

        await CreateHandler().Handle(new CreateDailyReadModelCommand(DateTime.UtcNow), CancellationToken.None);

        Assert.Equal(2, _writtenBatches.Count);
        Assert.Equal(100, _writtenBatches[0].Count);
        Assert.Single(_writtenBatches[1]);
    }

    [Fact]
    public async Task TwoFullBatches_BothSentAtBatchSize()
    {
        var ids = Enumerable.Range(0, 200).Select(_ => Guid.NewGuid()).ToList();
        SetUpWriterCapture();
        SetUpReader(ids);

        await CreateHandler().Handle(new CreateDailyReadModelCommand(DateTime.UtcNow), CancellationToken.None);

        Assert.Equal(2, _writtenBatches.Count);
        Assert.All(_writtenBatches, b => Assert.Equal(100, b.Count));
    }

    [Fact]
    public async Task PayloadMapping_UsesStockIdAndDateOnlyFromRequest()
    {
        var stockId = Guid.NewGuid();
        var requestDate = new DateTime(2026, 3, 5, 14, 30, 0, DateTimeKind.Utc);
        SetUpWriterCapture();
        SetUpReader([stockId]);

        await CreateHandler().Handle(new CreateDailyReadModelCommand(requestDate), CancellationToken.None);

        var (aggregateId, payload) = Assert.Single(Assert.Single(_writtenBatches));
        Assert.Equal(stockId, aggregateId);
        Assert.Equal(stockId, payload.AggregateId);
        Assert.Equal(DateOnly.FromDateTime(requestDate), payload.Date);
    }

    private void SetUpWriterCapture()
    {
        _outboxWriter
            .Setup(x => x.WriteManyAsync<DailyReadModelRequested>(
                It.IsAny<IEnumerable<(Guid AggregateId, DailyReadModelRequested Payload)>>(),
                It.IsAny<CancellationToken>()))
            .Returns<IEnumerable<(Guid, DailyReadModelRequested)>, CancellationToken>((batch, _) =>
            {
                _writtenBatches.Add(batch.Select(b => (b.Item1, b.Item2)).ToList());
                return Task.CompletedTask;
            });
    }

    private void SetUpReader(IEnumerable<Guid> ids)
    {
        _stockDataReader
            .Setup(x => x.GetAllIdsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns<int, CancellationToken>((_, ct) => ToAsyncEnumerable(ids, ct));
    }

    private static async IAsyncEnumerable<Guid> ToAsyncEnumerable(
        IEnumerable<Guid> ids,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        foreach (var id in ids)
        {
            ct.ThrowIfCancellationRequested();
            yield return id;
            await Task.Yield();
        }
    }
}