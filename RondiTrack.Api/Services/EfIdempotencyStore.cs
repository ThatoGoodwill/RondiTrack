using Microsoft.EntityFrameworkCore;
using RondiTrack.Domain;
using RondiTrack.Domain.Repositories;
using RondiTrack.Infrastructure.Data;

namespace RondiTrack.Api.Services;

// Same contract as the old in-memory store, but in PostgreSQL: it survives restarts and is shared by every replica.
public sealed class EfIdempotencyStore(RondiTrackDbContext db) : IIdempotencyStore
{
    public async Task<IdempotencyRecord?> FindAsync(string key, CancellationToken ct = default)
    {
        var entry = await db.IdempotencyKeys.AsNoTracking().FirstOrDefaultAsync(e => e.Key == key, ct);
        return entry is null
            ? null
            : new IdempotencyRecord(entry.Key, entry.RequestHash, entry.ResponseBodyJson, entry.CreatedAt);
    }

    public async Task SaveAsync(IdempotencyRecord record, CancellationToken ct = default)
    {
        db.IdempotencyKeys.Add(new IdempotencyKeyEntry(record.Key, record.RequestHash, record.ResponseBodyJson, record.CreatedAt));
        await db.SaveChangesAsync(ct);   // two simultaneous first-uses of one key: the primary key lets only one win
    }
}
