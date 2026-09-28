using RondiTrack.Api.Domain;

namespace RondiTrack.Api.Services;

// The relationship rule from Assignment 4.1, as a dedicated decision-maker.
public interface IMembershipService
{
    Task<Result<Membership>> AddMemberAsync(Guid stokvelId, Guid userId, CancellationToken ct = default);
    Task<Result> RemoveMemberAsync(Guid stokvelId, Guid userId, CancellationToken ct = default);
}
