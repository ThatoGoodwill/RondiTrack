namespace RondiTrack.Api.Jobs;

// Environment-variable form: CycleClose__Enabled, CycleClose__IntervalMinutes, CycleClose__TimeZoneId. None are secret.
public sealed class CycleCloseOptions
{
    public const string SectionName = "CycleClose";

    public bool Enabled { get; init; } = true;
    public int IntervalMinutes { get; init; } = 15;
    public string TimeZoneId { get; init; } = "Africa/Johannesburg";

    // Called at startup: a bad value stops the app immediately, with the setting's name in the message.
    public void EnsureValid()
    {
        if (IntervalMinutes < 1)
            throw new InvalidOperationException("CycleClose__IntervalMinutes must be 1 or more.");
        try { TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId); }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            throw new InvalidOperationException(
                $"CycleClose__TimeZoneId '{TimeZoneId}' is not a known time zone. Use an IANA id such as Africa/Johannesburg " +
                "(the container image must contain the tzdata package).", ex);
        }
    }
}