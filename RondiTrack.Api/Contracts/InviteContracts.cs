namespace RondiTrack.Api.Contracts;

// No user list, balance, payout order or account details may appear in this response.
public sealed record InvitePreviewResponse(
    Guid StokvelId, string Name, long ContributionCents, string Frequency,
    int MemberCount, int MaxMembers, string Rules, DateTimeOffset ExpiresAtUtc);

public sealed record InviteCreatedResponse(string Code, string JoinPath, DateTimeOffset ExpiresAtUtc);
public sealed record InviteJoinResponse(Guid StokvelId, bool AlreadyMember);
