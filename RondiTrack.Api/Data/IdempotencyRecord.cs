namespace RondiTrack.Api.Data;
// One row of the idempotency store. Deliberately holds the response as TEXT (JSON),
// so this store never needs to know what kind of operation it's protecting.
public sealed record IdempotencyRecord(string Key, string RequestHash, string ResponseBodyJson, DateTimeOffset CreatedAt);
