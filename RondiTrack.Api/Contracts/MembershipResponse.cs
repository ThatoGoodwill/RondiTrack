using RondiTrack.Api.Domain;

namespace RondiTrack.Api.Contracts;

// OUTGOING shape of one membership.
public sealed record MembershipResponse(Guid StokvelId, Guid UserId, string Role, DateTimeOffset JoinedAtUtc)
{
    public static MembershipResponse FromEntity(StokvelMember m) =>
        new(m.StokvelId, m.UserId, m.Role.ToString(), m.JoinedAtUtc);
}
