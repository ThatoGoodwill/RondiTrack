using System.Security.Claims;

namespace RondiTrack.Api.Auth;

// The claim names this API puts in its tokens: short, standard JWT names. MapInboundClaims = false
// in Program.cs keeps them that way instead of renaming them to long Microsoft URIs.
public static class AppClaims
{
    public const string Subject = "sub";
    public const string Role = "role";

    // The signed-in user's id (the "sub" claim), or null if the token has none / it is not a Guid.
    public static Guid? GetUserId(this ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirstValue(Subject), out var id) ? id : null;

    public static bool IsPlatformAdmin(this ClaimsPrincipal user) =>
        user.IsInRole(PlatformRoles.PlatformAdmin);
}
