using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace RondiTrack.Api.Services;

// Turns any request object into a short, stable "fingerprint" (SHA-256, hex text).
// Same request  -> always the same fingerprint.
// Different request -> a different fingerprint.
// We store it next to the Idempotency-Key so we can tell a genuine RETRY (same
// fingerprint) from a REUSED KEY with a different body (different fingerprint).
public static class IdempotencyHasher
{
    public static string Compute<T>(T request)
    {
        var json = JsonSerializer.Serialize(request);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        return Convert.ToHexString(bytes);
    }
}
