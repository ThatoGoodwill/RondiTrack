using Microsoft.AspNetCore.Authorization;
using RondiTrack.Api.Auth;
using RondiTrack.Domain;
using RondiTrack.Domain.Repositories;

namespace RondiTrack.Api.Authorization;

public sealed class RecordContributionHandler(IStokvelMemberRepository members)
    : AuthorizationHandler<RecordContributionRequirement, ContributionTarget>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context, RecordContributionRequirement requirement, ContributionTarget target)
    {
        if (context.User.IsPlatformAdmin())
        {
            context.Succeed(requirement);
            return;
        }

        var userId = context.User.GetUserId();
        if (userId is null) return;

        var membership = await members.GetAsync(target.StokvelId, userId.Value);

        if (membership is null)
            context.Fail(new AuthorizationFailureReason(this, StokvelFailure.NotMember));
        else if (target.TargetUserId == userId.Value || membership.Role >= StokvelMemberRole.Treasurer)
            context.Succeed(requirement);   // your own payment, or you are a treasurer/chairperson
        else
            context.Fail(new AuthorizationFailureReason(this, StokvelFailure.InsufficientRole));
    }
}
