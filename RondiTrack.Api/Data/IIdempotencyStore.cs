namespace RondiTrack.Api.Data;
public interface IIdempotencyStore
{
    Task<IdempotencyRecord?> FindAsync(string key, CancellationToken ct = default);
    Task SaveAsync(IdempotencyRecord record, CancellationToken ct = default);
}
