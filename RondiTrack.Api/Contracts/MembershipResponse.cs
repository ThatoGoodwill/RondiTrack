using RondiTrack.Api.Domain;
namespace RondiTrack.Api.Contracts;
public sealed record MembershipResponse(Guid UserId, DateTimeOffset JoinedAt)
{
    public static MembershipResponse FromEntity(Membership membership) =>
        new(membership.UserId, membership.JoinedAt);
}