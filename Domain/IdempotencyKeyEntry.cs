namespace RondiTrack.Domain;

// One remembered Idempotency-Key, now stored in PostgreSQL (was a dictionary in RAM), so it survives a restart
// and is shared by every replica. Housekeeping deletes entries older than the retention period.
public sealed class IdempotencyKeyEntry
{
    public IdempotencyKeyEntry(string key, string requestHash, string responseBodyJson, DateTimeOffset createdAt)
    {
        Key = key;
        RequestHash = requestHash;
        ResponseBodyJson = responseBodyJson;
        CreatedAt = createdAt;
    }

    public string Key { get; private set; }
    public string RequestHash { get; private set; }
    public string ResponseBodyJson { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
}