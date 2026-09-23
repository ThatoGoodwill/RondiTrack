using RondiTrack.Api.Data;
using RondiTrack.Api.Domain;
namespace RondiTrack.Api.Services;
public sealed class MembershipService(IStokvelRepository stokvels, IUserRepository users) : IMembershipService
{
    public async Task<Result<Membership>> AddMemberAsync(Guid stokvelId, Guid userId, CancellationToken ct = default)
    {
        var stokvel = await stokvels.GetByIdAsync(stokvelId, ct);
        if (stokvel is null)
            return Result<Membership>.Failure(new Error(ErrorType.NotFound, "stokvel.not_found",
                $"No stokvel with id {stokvelId} exists."));
        // Cross-entity check: needs to look at USERS, something the Stokvel entity has no way to do.
        if (!await users.ExistsAsync(userId, ct))
            return Result<Membership>.Failure(new Error(ErrorType.Unprocessable, "membership.user_not_found",
                $"No user with id {userId} exists."));
        // The Stokvel's OWN rule (duplicate / full) -- the entity still enforces this itself.
        var result = stokvel.AddMember(userId);
        if (!result.IsSuccess) return Result<Membership>.Failure(result.Error);
        return Result<Membership>.Success(stokvel.GetMember(userId)!);
    }
    public async Task<Result> RemoveMemberAsync(Guid stokvelId, Guid userId, CancellationToken ct = default)
    {
        var stokvel = await stokvels.GetByIdAsync(stokvelId, ct);
        if (stokvel is null)
            return Result.Failure(new Error(ErrorType.NotFound, "stokvel.not_found",
                $"No stokvel with id {stokvelId} exists."));
        return stokvel.RemoveMember(userId);   // "not a member" -> NotFound, handled by the entity itself
    }
}
