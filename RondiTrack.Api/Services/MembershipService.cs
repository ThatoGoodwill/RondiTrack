using RondiTrack.Api.Data;
using RondiTrack.Api.Domain;

namespace RondiTrack.Api.Services;

// Adding a member spans TWO entities (Stokvel + User), so it is a service decision:
//   1. does the stokvel exist?                       -> NotFound   (404)
//   2. does the user exist?                          -> Unprocessable (422)
//   3. duplicate member / stokvel full?              -> Conflict (409), decided by the Stokvel ENTITY
public sealed class MembershipService(IStokvelRepository stokvels, IUserRepository users) : IMembershipService
{
    public async Task<Result<Membership>> AddMemberAsync(Guid stokvelId, Guid userId, CancellationToken ct = default)
    {
        var stokvel = await stokvels.GetByIdAsync(stokvelId, ct);
        if (stokvel is null)
            return Result<Membership>.Failure(new Error(ErrorType.NotFound, "stokvel.not_found",
                $"No stokvel with id {stokvelId} exists."));

        // The Stokvel cannot look at users; that is why this check lives in the service.
        if (!await users.ExistsAsync(userId, ct))
            return Result<Membership>.Failure(new Error(ErrorType.Unprocessable, "membership.user_not_found",
                $"No user with id {userId} exists."));

        // The Stokvel's OWN rules (no duplicates, not full) stay inside the entity.
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

        return stokvel.RemoveMember(userId);   // "not a member" comes back as NotFound from the entity
    }
}
