using System.Text.Json.Serialization;

namespace RondiTrack.Api.Domain;

// A CLOSED list of allowed schedules. Serialised as text ("Monthly") not a number.
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ContributionFrequency
{
    // Starts at 1 on purpose: default(ContributionFrequency) == 0 is then NOT valid,
    // so a missing "frequency" in JSON is rejected instead of silently becoming Weekly.
    Weekly = 1,
    Fortnightly = 2,
    Monthly = 3
}
