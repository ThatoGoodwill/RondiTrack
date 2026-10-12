using RondiTrack.Domain;
using RondiTrack.Domain.Repositories;

namespace RondiTrack.Api.Services;

// Adding a member spans TWO entities (Stokvel + User), so it is a service decision:
//   1. does the stokvel exist?                       -> NotFound   (404)
//   2. does the user exist?                          -> Unprocessable (422)
//   3. duplicate member / stokvel full?              -> Conflict (409), decided by the Stokvel ENTITY
public sealed class MembershipService(
    IStokvelRepository stokvels, IUserRepository users, IStokvelMemberRepository memberRepository) : IMembershipService
{
    public async Task<Result<StokvelMember>> AddMemberAsync(Guid stokvelId, Guid userId, CancellationToken ct = default)
    {
        var stokvel = await stokvels.GetByIdAsync(stokvelId, ct);
        if (stokvel is null)
            return Result<StokvelMember>.Failure(new Error(ErrorType.NotFound, "stokvel.not_found",
                $"No stokvel with id {stokvelId} exists."));

        // The Stokvel cannot look at users; that is why this check lives in the service.
        if (!await users.ExistsAsync(userId, ct))
            return Result<StokvelMember>.Failure(new Error(ErrorType.Unprocessable, "membership.user_not_found",
                $"No user with id {userId} exists."));

        // The Stokvel's OWN rules (no duplicates, not full) stay inside the entity.
        var result = stokvel.AddMember(userId);
        if (!result.IsSuccess) return Result<StokvelMember>.Failure(result.Error);

        // 6.1: members are in PostgreSQL now, so the change must be saved. A simultaneous duplicate
        // add hits the composite primary key and comes back as 409 from the exception handler.
        var membership = stokvel.GetMember(userId)!;
        await memberRepository.AddAsync(membership, ct);   // marks it Added explicitly, then saves

        return Result<StokvelMember>.Success(membership);
    }

    public async Task<Result> RemoveMemberAsync(Guid stokvelId, Guid userId, CancellationToken ct = default)
    {
        var stokvel = await stokvels.GetByIdAsync(stokvelId, ct);
        if (stokvel is null)
            return Result.Failure(new Error(ErrorType.NotFound, "stokvel.not_found",
                $"No stokvel with id {stokvelId} exists."));

        var result = stokvel.RemoveMember(userId);   // "not a member" comes back as NotFound from the entity
        if (!result.IsSuccess) return result;

        await stokvels.SaveChangesAsync(ct);          // 6.1: persist the removal
        return Result.Success();
    }
}
