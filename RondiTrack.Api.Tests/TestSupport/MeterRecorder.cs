using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.DependencyInjection;

namespace RondiTrack.Api.Tests.TestSupport;

public sealed record RecordedMeasurement(string Instrument, long Value, IReadOnlyDictionary<string, object?> Tags);

// Listens to ONE meter of ONE app instance (built-in MeterListener, no extra package).
//
// Why the Scope check: the app creates its meter through IMeterFactory, and that factory is the meter's Scope. Comparing
// scopes means this recorder only sees THIS factory's app, even if another test host in the same process
// records to a meter with the same name.
public sealed class MeterRecorder : IDisposable
{
    private readonly MeterListener _listener = new();
    private readonly ConcurrentQueue<RecordedMeasurement> _measurements = new();

    public MeterRecorder(IServiceProvider appServices, string meterName)
    {
        var scope = appServices.GetRequiredService<IMeterFactory>();

        _listener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Name == meterName && ReferenceEquals(instrument.Meter.Scope, scope))
                listener.EnableMeasurementEvents(instrument);
        };

        _listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
        {
            var copy = new Dictionary<string, object?>();
            foreach (var tag in tags) copy[tag.Key] = tag.Value;
            _measurements.Enqueue(new RecordedMeasurement(instrument.Name, value, copy));
        });

        _listener.Start();   // also reports instruments that already exist
    }

    public IReadOnlyList<RecordedMeasurement> Measurements => _measurements.ToArray();

    public void Dispose() => _listener.Dispose();
}
