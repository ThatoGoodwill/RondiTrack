namespace RondiTrack.Api.Authorization;

// Put on an action (or controller) under /api/stokvels/{stokvelId}/... to name the POLICY it needs.
// It carries a policy NAME, never a role name: the rule itself lives in Program.cs.
// No attribute = Policies.StokvelMember (fail closed: a forgotten attribute still demands membership).
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class StokvelAccessAttribute : Attribute
{
    public StokvelAccessAttribute(string policy) => Policy = policy;
    public string Policy { get; }
}
