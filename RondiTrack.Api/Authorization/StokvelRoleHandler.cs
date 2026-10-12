using Microsoft.AspNetCore.Authorization;
using RondiTrack.Api.Auth;
using RondiTrack.Domain;
using RondiTrack.Domain.Repositories;

namespace RondiTrack.Api.Authorization;

// Resource-based handler: decides "may THIS caller do this in THIS stokvel?" by reading the caller's
// StokvelMember row from the DATABASE on every request. Nothing about stokvel roles is trusted from
// the token, so a role removed at 10:00 stops working at 10:00, not when the token expires.
public sealed class StokvelRoleHandler(IStokvelMemberRepository members)
    : AuthorizationHandler<StokvelRoleRequirement, StokvelResource>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context, StokvelRoleRequirement requirement, StokvelResource resource)
    {
        // Platform admins operate the platform; they may act in any stokvel (see README: decision + gap).
        if (context.User.IsPlatformAdmin())
        {
            context.Succeed(requirement);
            return;
        }

        var userId = context.User.GetUserId();
        if (userId is null) return;   // no usable "sub": not succeeding IS failing

        var membership = await members.GetAsync(resource.StokvelId, userId.Value);

        if (membership is null)
            context.Fail(new AuthorizationFailureReason(this, StokvelFailure.NotMember));
        else if (membership.Role >= requirement.MinimumRole)
            context.Succeed(requirement);
        else
            context.Fail(new AuthorizationFailureReason(this, StokvelFailure.InsufficientRole));
    }
}
