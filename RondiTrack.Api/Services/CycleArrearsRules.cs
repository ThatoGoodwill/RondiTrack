namespace RondiTrack.Api.Services;

// The background close job and the preview/confirm endpoint share the exact shortfall rule.
public static class CycleArrearsRules
{
    public static long ShortfallCents(long targetCents, long paidCents) =>
        Math.Max(0L, targetCents - paidCents);
}
