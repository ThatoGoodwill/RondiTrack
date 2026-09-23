using RondiTrack.Api.Domain;
namespace RondiTrack.Api.Contracts;
// Note MemberCount, an int, instead of the full Members collection -
// a deliberate choice: see the assignment brief and the Concepts Guide, Chapter 2.4.
public sealed record StokvelResponse(
    Guid Id, string Name, decimal ContributionAmount, ContributionFrequency Frequency,
    int MaxMembers, int MemberCount)
{
    public static StokvelResponse FromEntity(Stokvel stokvel) =>
        new(stokvel.Id, stokvel.Name, stokvel.ContributionAmount, stokvel.Frequency,
            stokvel.MaxMembers, stokvel.MemberCount);
}