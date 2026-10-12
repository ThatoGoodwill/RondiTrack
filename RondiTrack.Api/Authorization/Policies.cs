namespace RondiTrack.Api.Authorization;

// Named policies. A controller says WHAT it needs ("a treasurer of this stokvel"); Program.cs and the
// handlers are the one place that says HOW that is decided. Change the rule there and every endpoint
// using the policy follows.
public static class Policies
{
    // Platform-level (from the token's role claim)
    public const string PlatformAdmin = "PlatformAdmin";

    // Stokvel-scoped (from the database, per stokvel). Roles are ordered Member < Treasurer < Admin.
    public const string StokvelMember = "StokvelMember";            // any member
    public const string StokvelTreasurer = "StokvelTreasurer";      // Treasurer or Chairperson (Admin)
    public const string StokvelChairperson = "StokvelChairperson";  // Admin only

    // "May this caller record a contribution for THIS member?" (yourself, or anyone if treasurer+)
    public const string RecordContribution = "RecordContribution";
}
