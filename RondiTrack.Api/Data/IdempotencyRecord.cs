namespace RondiTrack.Api.Data;

// One remembered entry in the idempotency store:
//   Key              = the Idempotency-Key header the client sent
//   RequestHash      = a fingerprint of the request (to tell a retry from a reused key)
//   ResponseBodyJson = the EXACT response we gave, stored as text (so the store stays generic)
//   CreatedAt        = when we first saw the key
public sealed record IdempotencyRecord(string Key, string RequestHash, string ResponseBodyJson, DateTimeOffset CreatedAt);
