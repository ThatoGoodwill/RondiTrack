using System.Text.Json;

namespace RondiTrack.Api.Exceptions;

public static class FieldNames
{
   
    // "Password" -> "password", "Items[0].UnitPrice" -> "items[0].unitPrice", "$.amount" -> "amount":
    // the names the CLIENT sent, so a form can put each message next to its input. 
    
      public static string ToCamelPath(string path)
      {
        if(path.StartsWith("$.")) path = path[2..];
        return string.Join('.', path.Split('.').Select(segment =>
        {
            var bracket = segment.IndexOf('[');
            var name = bracket < 0 ? segment : segment[..bracket];
            var rest = bracket < 0 ? "" : segment[bracket..];
            return JsonNamingPolicy.CamelCase.ConvertName(name) + rest;
        }));
      }

}