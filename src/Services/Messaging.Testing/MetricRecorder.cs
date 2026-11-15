using System.Diagnostics.Metrics;

namespace Messaging.Testing;

public sealed record RecordedMeasurement(string Instrument, double Value, IReadOnlyDictionary<string, object?> Tags);

public sealed class MetricRecorder : IDisposable
{
    private readonly MeterListener _listener = new();
    private readonly List<RecordedMeasurement> _measurements = [];

    public MetricRecorder()
    {
        _listener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Name == MessagingTelemetry.MeterName)
                listener.EnableMeasurementEvents(instrument);
        };
        _listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) => Add(instrument, value, tags));
        _listener.SetMeasurementEventCallback<double>((instrument, value, tags, _) => Add(instrument, value, tags));
        _listener.Start();
    }

    public IReadOnlyList<RecordedMeasurement> For(string instrument, string topic)
    {
        lock (_measurements)
            return _measurements
                .Where(m => m.Instrument == instrument &&
                            Equals(m.Tags.GetValueOrDefault("messaging.destination.name"), topic))
                .ToList();
    }

    public IReadOnlyList<RecordedMeasurement> For(string instrument)
    {
        lock (_measurements) return _measurements.Where(m => m.Instrument == instrument).ToList();
    }

    public void Observe()
    {
        _listener.RecordObservableInstruments();
    }

    public void Dispose()
    {
        _listener.Dispose();
    }

    private void Add(Instrument instrument, double value, ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        var measurement = new RecordedMeasurement(instrument.Name, value,
            tags.ToArray().ToDictionary(t => t.Key, t => t.Value));
        lock (_measurements) _measurements.Add(measurement);
    }
}
