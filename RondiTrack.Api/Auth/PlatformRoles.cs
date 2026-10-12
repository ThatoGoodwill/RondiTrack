namespace RondiTrack.Api.Auth;

// PLATFORM-level roles: facts about a person across the whole app. They change rarely, so they
// are safe to carry in an access token that may be up to 10 minutes stale.
//
// Deliberately NOT here: Member / Treasurer / Admin. Those are per-STOKVEL roles (see
// StokvelMemberRole) and are always looked up in the database.
public static class PlatformRoles
{
    public const string User = "User";
    public const string PlatformAdmin = "PlatformAdmin";
}
