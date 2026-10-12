using Microsoft.AspNetCore.Mvc;

namespace RondiTrack.Api.Authorization;

// One-line helpers for the few actions that cannot be decided from the route alone.
public static class ControllerAuthorizationExtensions
{
    // For routes that have no {stokvelId} (e.g. /api/payouts/{id}): load the thing first, then ask
    // about ITS stokvel.
    public static Task EnsureStokvelAccessAsync(
        this ControllerBase controller, Guid stokvelId, string policy,
        string? notFoundCode = null, string? notFoundMessage = null) =>
        Checker(controller).EnsureAsync(controller.User, stokvelId, policy, notFoundCode, notFoundMessage);

    // For POST .../contributions: the body says whose payment it is.
    public static Task EnsureCanRecordContributionAsync(this ControllerBase controller, Guid stokvelId, Guid targetUserId) =>
        Checker(controller).EnsureCanRecordContributionAsync(controller.User, stokvelId, targetUserId);

    private static StokvelAccessChecker Checker(ControllerBase controller) =>
        controller.HttpContext.RequestServices.GetRequiredService<StokvelAccessChecker>();
}
