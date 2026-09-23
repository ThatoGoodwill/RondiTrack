using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
namespace RondiTrack.Api.Services;
public static class IdempotencyHasher
{
    // Turns any request object into a short, stable fingerprint (SHA-256, hex text).
    // Same input -> always the same hash. Different input -> (for all practical purposes) a different hash.
    public static string Compute<T>(T request)
    {
        var json = JsonSerializer.Serialize(request);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        return Convert.ToHexString(bytes);
    }
}