namespace RondiTrack.Api.Domain;

public enum StokvelMemberRole { Member = 1, Treasurer = 2, Admin = 3 }

public sealed class StokvelMember
{
    private StokvelMember(Guid stokvelId, Guid userId, StokvelMemberRole role, DateTimeOffset joinedAtUtc)
    {
        StokvelId = stokvelId;
        UserId = userId;
        Role = role;
        JoinedAtUtc = joinedAtUtc;
    }

    public Guid StokvelId { get; }
    public Guid UserId { get; }
    public StokvelMemberRole Role { get; private set; }
    public DateTimeOffset JoinedAtUtc { get; }

    public static StokvelMember Create(Guid stokvelId, Guid userId, StokvelMemberRole role = StokvelMemberRole.Member) =>
        new(stokvelId, userId, role, DateTimeOffset.UtcNow);

    public void ChangeRole(StokvelMemberRole newRole) => Role = newRole;
}