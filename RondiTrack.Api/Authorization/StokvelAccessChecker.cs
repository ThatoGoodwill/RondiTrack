using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using RondiTrack.Api.Observability;
using RondiTrack.Domain.Exceptions;

namespace RondiTrack.Api.Authorization;

// The ONE place that turns an authorization result into an HTTP-meaningful exception:
//   not a member            -> 404 (same code and message as a stokvel that really does not exist)
//   member, role too low    -> 403
// No try/catch anywhere: handlers decide, this class translates, the exception handler answers.
//
// 6.2: it is also the ONE place that COUNTS stokvel authorization refusals.
public sealed class StokvelAccessChecker(IAuthorizationService authorization, RondiTrackMetrics metrics)
{
    // notFoundCode / notFoundMessage let a caller that reached the stokvel THROUGH another resource
    // (a payout) answer "not a member" with that resource's own 404, so nothing about the stokvel leaks.
    public async Task EnsureAsync(
        ClaimsPrincipal user, Guid stokvelId, string policy, string? notFoundCode = null, string? notFoundMessage = null)
    {
        var result = await authorization.AuthorizeAsync(user, new StokvelResource(stokvelId), policy);
        ThrowIfDenied(user, policy, result, stokvelId, notFoundCode, notFoundMessage);
    }

    public async Task EnsureCanRecordContributionAsync(ClaimsPrincipal user, Guid stokvelId, Guid targetUserId)
    {
        var result = await authorization.AuthorizeAsync(
            user, new ContributionTarget(stokvelId, targetUserId), Policies.RecordContribution);
        ThrowIfDenied(user, Policies.RecordContribution, result, stokvelId);
    }

    private void ThrowIfDenied(
        ClaimsPrincipal user, string policy, AuthorizationResult result, Guid stokvelId,
        string? notFoundCode = null, string? notFoundMessage = null)
    {
        if (result.Succeeded) return;

        var notMember = result.Failure?.FailureReasons.Any(r => r.Message == StokvelFailure.NotMember) == true;
        if (notMember)
        {
            metrics.AuthorizationRefused(RefusalResults.NotFound);
            throw new NotFoundException(
                notFoundCode ?? "stokvel.not_found",
                notFoundMessage ?? $"No stokvel with id {stokvelId} exists.");
        }

        metrics.AuthorizationRefused(RefusalResults.Forbidden);
        throw new ForbiddenException("stokvel.forbidden", "Your role in this stokvel does not allow this action.");
    }
}
