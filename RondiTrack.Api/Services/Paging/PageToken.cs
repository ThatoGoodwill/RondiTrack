using System.Text;
using System.Text.Json;

namespace RondiTrack.Api.Services.Paging;

// The token is OPAQUE to the client: it's base64 text with no documented meaning.
// Internally it carries the last row's sort value, its Id as a tiebreaker, and a
// fingerprint of the sort+filter that produced it, so reusing a token with a
// DIFFERENT sort or filter can be detected and rejected rather than silently
// returning wrong results.
  public sealed record PageTokenData(string LastSortValue, Guid LastId, string Fingerprint ) ;

  public static class PageToken
  {
       
       public static string Encode(PageTokenData data)
       {
        var json = JsonSerializer.Serialize(data);
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
       }

       public static PageTokenData? Decode(string? token)
       {
        if(string.IsNullOrWhiteSpace(token)) return null;
        try
        {
            var json = Encoding.UTF8.GetString(Convert.FromBase64String(token));
            return JsonSerializer.Deserialize<PageTokenData>(json);
        }
        catch
        {
            return null;   // a malformed token is treated as invalid, not as a crash
        }
    }
    public static string Fingerprint(string sort, string? filter) => $"{sort}|{filter ?? ""}";

  }