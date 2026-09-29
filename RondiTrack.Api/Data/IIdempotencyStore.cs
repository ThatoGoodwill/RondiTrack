namespace RondiTrack.Api.Data;

// "Have I already handled this key, and what did I answer?"  Not business data, just memory.
public interface IIdempotencyStore
{
    Task<IdempotencyRecord?> FindAsync(string key, CancellationToken ct = default);
    Task SaveAsync(IdempotencyRecord record, CancellationToken ct = default);
}
