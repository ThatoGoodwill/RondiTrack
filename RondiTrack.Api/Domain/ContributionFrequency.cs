using System.Text.Json.Serialization;
namespace RondiTrack.Api.Domain;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ContributionFrequency { Weekly = 1, Fortnightly = 2, Monthly = 3 }   // starts at 1 on purpose