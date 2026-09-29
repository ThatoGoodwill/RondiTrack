using RondiTrack.Api.Domain;

namespace RondiTrack.Api.Contracts;

// OUTGOING shape of a stokvel. Note MemberCount (a number) instead of the whole member list:
// most callers only need the count; the full list lives at GET /api/stokvels/{id}/members.
public sealed record StokvelResponse(
    Guid Id, string Name, decimal ContributionAmount, ContributionFrequency Frequency,
    int MaxMembers, int MemberCount)
{
    public static StokvelResponse FromEntity(Stokvel stokvel) =>
        new(stokvel.Id, stokvel.Name, stokvel.ContributionAmount, stokvel.Frequency,
            stokvel.MaxMembers, stokvel.MemberCount);
}
