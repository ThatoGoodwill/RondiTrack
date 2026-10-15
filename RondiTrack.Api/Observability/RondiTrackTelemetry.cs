using System.Diagnostics;

namespace RondiTrack.Api.Observability;

// The names RondiTrack uses for its OWN traces. ASP.NET Core and Npgsql bring their own sources; this one is ours.
public static class RondiTrackTelemetry
{
    // Must be registered in Program.cs with tracing.AddSource(ActivitySourceName). A source nobody registers
    // produces spans nobody collects: StartActivity returns null and nothing is recorded.
    public const string ActivitySourceName = "RondiTrack";

    public static readonly ActivitySource ActivitySource = new(ActivitySourceName);

    // The span for processing a payout: named for the BUSINESS operation, not for the method that implements it.
    public const string PayoutProcessSpan = "payout.process";

    // Span tags. Every one is listed, with why it is safe and what it gives a support engineer, in the README.
    public static class PayoutTags
    {
        public const string StokvelId = "ronditrack.stokvel.id";
        public const string CycleId = "ronditrack.cycle.id";
        public const string PayoutId = "ronditrack.payout.id";
        public const string Outcome = "ronditrack.payout.outcome";
        public const string ErrorType = "error.type";
    }
}
