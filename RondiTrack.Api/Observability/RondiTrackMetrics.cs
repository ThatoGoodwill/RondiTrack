using System.Diagnostics.Metrics;

namespace RondiTrack.Api.Observability;

// RondiTrack's business metrics: things ASP.NET Core cannot know ("a payout", "a refused member").
//
// THE TAG RULE. A tag is a dimension, and the metrics backend keeps one time series per distinct value.
// So a tag must come from a SMALL, FIXED set that this file defines. Never a stokvel id, a user id, an amount
// or a name: those would create one series per person (unbounded), and put personal information into a system
// that was never meant to hold it. Every tag below lists the number of values it can ever take.
//
// Created once (Singleton) from IMeterFactory, so tests can listen to this meter without touching other tests.
// Program.cs must register the name with metrics.AddMeter(MeterName), or these counters record into the void.
public sealed class RondiTrackMetrics
{
    public const string MeterName = "RondiTrack";

    public const string ContributionsRecordedName = "ronditrack.contributions.recorded";
    public const string PayoutsProcessedName = "ronditrack.payouts.processed";
    public const string AuthorizationRefusalsName = "ronditrack.authorization.refusals";
    public const string SignInsName = "ronditrack.auth.sign_ins";
    public const string RefreshTokenReuseName = "ronditrack.auth.refresh_token_reuse";

    public const string OutcomeTag = "outcome";
    public const string ResultTag = "result";

    private readonly Counter<long> _contributionsRecorded;
    private readonly Counter<long> _payoutsProcessed;
    private readonly Counter<long> _authorizationRefusals;
    private readonly Counter<long> _signIns;
    private readonly Counter<long> _refreshTokenReuse;

    public RondiTrackMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName);

        _contributionsRecorded = meter.CreateCounter<long>(
            ContributionsRecordedName, unit: "{contribution}",
            description: "Contributions newly recorded. An idempotent replay is not counted.");

        _payoutsProcessed = meter.CreateCounter<long>(
            PayoutsProcessedName, unit: "{payout}",
            description: "Payout attempts, tagged by outcome.");

        _authorizationRefusals = meter.CreateCounter<long>(
            AuthorizationRefusalsName, unit: "{refusal}",
            description: "Authorization refusals, tagged by result (not_found or forbidden).");

        _signIns = meter.CreateCounter<long>(
            SignInsName, unit: "{attempt}",
            description: "Sign-in attempts, tagged by outcome (success or failure).");

        _refreshTokenReuse = meter.CreateCounter<long>(
            RefreshTokenReuseName, unit: "{event}",
            description: "Refresh tokens presented after they were already spent (probable theft).");
    }

    // No tags: one time series.
    public void ContributionRecorded() => _contributionsRecorded.Add(1);

    // Tag "outcome": at most 8 distinct values (see PayoutOutcomes).
    public void PayoutProcessed(string outcome) =>
        _payoutsProcessed.Add(1, new KeyValuePair<string, object?>(OutcomeTag, outcome));

    // Tag "result": exactly 2 distinct values (see RefusalResults).
    public void AuthorizationRefused(string result) =>
        _authorizationRefusals.Add(1, new KeyValuePair<string, object?>(ResultTag, result));

    // Tag "outcome": exactly 2 distinct values (see SignInOutcomes).
    public void SignInAttempted(bool succeeded) =>
        _signIns.Add(1, new KeyValuePair<string, object?>(
            OutcomeTag, succeeded ? SignInOutcomes.Success : SignInOutcomes.Failure));

    // No tags: one time series.
    public void RefreshTokenReuseDetected() => _refreshTokenReuse.Add(1);
}

// The ONLY values the payout "outcome" tag (and the payout span's outcome tag) can take: 8.
public static class PayoutOutcomes
{
    public const string Success = "success";
    public const string CycleNotFound = "cycle_not_found";
    public const string AlreadyProcessed = "already_processed";
    public const string RotationComplete = "rotation_complete";
    public const string NoContributions = "no_contributions";
    public const string OtherRefused = "other_refused";   // a business refusal this list does not know by name
    public const string Cancelled = "cancelled";          // the caller went away
    public const string Error = "error";                  // an unexpected exception: a bug

    // Maps a payout service error code to one of the fixed values. An unknown code can NEVER create a new
    // tag value: it becomes "other_refused".
    public static string FromErrorCode(string code) => code switch
    {
        "cycle.not_found" => CycleNotFound,
        "payout.already_processed" => AlreadyProcessed,
        "payout.rotation_complete" => RotationComplete,
        "payout.no_contributions" => NoContributions,
        _ => OtherRefused
    };
}

// The ONLY values the authorization-refusal "result" tag can take: 2.
public static class RefusalResults
{
    public const string NotFound = "not_found";    // not a member: answered with 404
    public const string Forbidden = "forbidden";   // a member (or an ordinary user) whose role is not enough: 403
}

// The ONLY values the sign-in "outcome" tag can take: 2.
public static class SignInOutcomes
{
    public const string Success = "success";
    public const string Failure = "failure";
}
