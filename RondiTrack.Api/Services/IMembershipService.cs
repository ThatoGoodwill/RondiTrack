using RondiTrack.Api.Domain;
namespace RondiTrack.Api.Services;
public interface IMembershipService
{
    Task<Result<Membership>> AddMemberAsync(Guid stokvelId, Guid userId, CancellationToken ct = default);
    Task<Result> RemoveMemberAsync(Guid stokvelId, Guid userId, CancellationToken ct = default);
}