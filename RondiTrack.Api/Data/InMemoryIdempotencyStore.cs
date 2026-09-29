using System.Collections.Concurrent;

namespace RondiTrack.Api.Data;

// In-memory store of seen Idempotency-Keys (a dictionary in RAM; a database table later).
public sealed class InMemoryIdempotencyStore : IIdempotencyStore
{
    private readonly ConcurrentDictionary<string, IdempotencyRecord> _records = new();

    public Task<IdempotencyRecord?> FindAsync(string key, CancellationToken ct = default)
    {
        _records.TryGetValue(key, out var record);
        return Task.FromResult<IdempotencyRecord?>(record);
    }

    public Task SaveAsync(IdempotencyRecord record, CancellationToken ct = default)
    {
        _records[record.Key] = record;
        return Task.CompletedTask;
    }
}
