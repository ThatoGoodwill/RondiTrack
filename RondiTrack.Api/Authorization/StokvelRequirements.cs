using Microsoft.AspNetCore.Authorization;
using RondiTrack.Domain;

namespace RondiTrack.Api.Authorization;

// The RESOURCE the caller is trying to touch: one stokvel.
public sealed record StokvelResource(Guid StokvelId);

// The resource for recording a contribution: one stokvel AND whose payment it is.
public sealed record ContributionTarget(Guid StokvelId, Guid TargetUserId);

// "The caller must hold AT LEAST this role in THIS stokvel."
public sealed class StokvelRoleRequirement(StokvelMemberRole minimumRole) : IAuthorizationRequirement
{
    public StokvelMemberRole MinimumRole { get; } = minimumRole;
}

// "The caller may record their OWN contribution; recording someone else's needs Treasurer or above."
public sealed class RecordContributionRequirement : IAuthorizationRequirement;

// Why a handler said no. The checker turns NotMember into 404 and InsufficientRole into 403.
public static class StokvelFailure
{
    public const string NotMember = "stokvel.not_member";
    public const string InsufficientRole = "stokvel.insufficient_role";
}
