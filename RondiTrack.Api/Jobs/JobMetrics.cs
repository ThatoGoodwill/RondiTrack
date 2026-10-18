using System.Diagnostics.Metrics;
using RondiTrack.Api.Observability;

namespace RondiTrack.Api.Jobs;

// Counters for the background jobs. No tag carries a stokvel id, user id or name (see the tag rule in RondiTrackMetrics).
public sealed class JobMetrics
{
    private readonly Counter<long> _cyclesClosed;
    private readonly Counter<long> _arrearsRecorded;
    private readonly Counter<long> _cycleCloseFailures;
    private readonly Counter<long> _housekeepingDeleted;

    public JobMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(RondiTrackMetrics.MeterName);
        _cyclesClosed = meter.CreateCounter<long>("ronditrack.cycles.closed", "{cycle}", "Contribution cycles closed by the job.");
        _arrearsRecorded = meter.CreateCounter<long>("ronditrack.arrears.recorded", "{record}", "Arrears records written when cycles close.");
        _cycleCloseFailures = meter.CreateCounter<long>("ronditrack.cycle_close.failures", "{failure}", "Stokvels whose close was rolled back and will be retried.");
        _housekeepingDeleted = meter.CreateCounter<long>("ronditrack.housekeeping.deleted", "{row}", "Rows deleted by housekeeping, tagged by kind (2 possible values).");
    }

    public void CycleClosed() => _cyclesClosed.Add(1);
    public void ArrearsRecorded(long count) => _arrearsRecorded.Add(count);
    public void CycleCloseFailed() => _cycleCloseFailures.Add(1);
    public void Deleted(string kind, long count) =>
        _housekeepingDeleted.Add(count, new KeyValuePair<string, object?>("kind", kind));
}

public static class HousekeepingKinds
{
    public const string RefreshTokens = "refresh_tokens";
    public const string IdempotencyKeys = "idempotency_keys";
}