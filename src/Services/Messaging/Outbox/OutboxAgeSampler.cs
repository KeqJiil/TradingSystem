using System.Diagnostics.Metrics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Messaging.Outbox;

internal class OutboxAgeSampler : BackgroundService
{
    private readonly ILogger<OutboxAgeSampler> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeSpan _interval;
    private readonly Meter _meter = new(MessagingTelemetry.MeterName);
    private double _ageSeconds;

    public OutboxAgeSampler(ILogger<OutboxAgeSampler> logger, IServiceScopeFactory scopeFactory,
        IOptions<OutboxRelayOptions> options)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
        _interval = options.Value.AgeMetricInterval
                    ?? throw new InvalidOperationException(
                        $"{nameof(OutboxRelayOptions)}.{nameof(OutboxRelayOptions.AgeMetricInterval)} must be set.");

        _meter.CreateObservableGauge(
            "messaging.outbox.oldest_age",
            () => Volatile.Read(ref _ageSeconds),
            unit: "s",
            description: "Age of the oldest outbox row that is not yet published.");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(_interval);
        do
        {
            try
            {
                await SampleAsync(stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "An unexpected error occurred while sampling the outbox age");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    public override void Dispose()
    {
        _meter.Dispose();
        base.Dispose();
    }

    private async Task SampleAsync(CancellationToken ct)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IOutboxStore>();

        var oldest = await store.GetOldestUnfinishedAtAsync(ct);
        var age = oldest is null ? 0 : Math.Max(0, (DateTimeOffset.UtcNow - oldest.Value).TotalSeconds);
        Volatile.Write(ref _ageSeconds, age);
    }
}
