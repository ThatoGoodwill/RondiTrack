using System.Diagnostics;
using RondiTrack.Api.Observability;
using RondiTrack.Domain;
using RondiTrack.Domain.Exceptions;

namespace RondiTrack.Api.Services;

// Wraps PayoutService (the class that owns the operation) in a span of RondiTrack's own: "payout.process".
//
// WHY A SPAN: when a treasurer says "the payout just failed", the request span only says POST .../payout took 80 ms
// and returned 409. This span says WHICH payout, and how it ended, and (because Npgsql's SQL spans are created while
// this span is the current activity) it is the parent of every SQL command the payout ran. In the dashboard the
// payout's SQL statements sit nested under it, inside the request span.
//
// TAGS (each is justified in the README): stokvel id, cycle id, outcome, payout id (on success only), and on an
// unexpected exception the exception's TYPE NAME. Nothing else. In particular NOT the amount, the recipient, a name,
// an email or an exception message: any of those next to a stokvel id is a record of a named family's finances.
//
// Registered in Program.cs. No telemetry code lives in the controller.
public sealed class InstrumentedPayoutService(IPayoutService inner, RondiTrackMetrics metrics) : IPayoutService
{
    public async Task<Result<Payout>> ProcessNextPayoutAsync(
        Guid stokvelId, Guid contributionCycleId, CancellationToken ct = default)
    {
        // Null when nobody is listening (no tracing configured). Every use below is null-safe.
        using var activity = RondiTrackTelemetry.ActivitySource.StartActivity(RondiTrackTelemetry.PayoutProcessSpan);
        activity?.SetTag(RondiTrackTelemetry.PayoutTags.StokvelId, stokvelId.ToString());
        activity?.SetTag(RondiTrackTelemetry.PayoutTags.CycleId, contributionCycleId.ToString());

        try
        {
            var result = await inner.ProcessNextPayoutAsync(stokvelId, contributionCycleId, ct);

            if (result.IsSuccess)
            {
                activity?.SetTag(RondiTrackTelemetry.PayoutTags.PayoutId, result.Value.Id.ToString());
                Finish(activity, PayoutOutcomes.Success, failed: false);
            }
            else
            {
                Finish(activity, PayoutOutcomes.FromErrorCode(result.Error.Code), failed: true);
            }

            return result;
        }
        catch (OperationCanceledException)
        {
            Finish(activity, PayoutOutcomes.Cancelled, failed: true);
            throw;
        }
        catch (Exception exception)
        {
            // A refusal that arrives as one of our own exceptions keeps its named outcome; anything else is a bug.
            // The span records the exception's TYPE only: its message could contain data.
            var outcome = exception is RondiTrackException known
                ? PayoutOutcomes.FromErrorCode(known.Code)
                : PayoutOutcomes.Error;
            activity?.SetTag(RondiTrackTelemetry.PayoutTags.ErrorType, exception.GetType().FullName);
            Finish(activity, outcome, failed: true);
            throw;
        }
    }

    // One place that ends the story: outcome tag, span status, counter. A failed payout ALWAYS gets Error status.
    private void Finish(Activity? activity, string outcome, bool failed)
    {
        activity?.SetTag(RondiTrackTelemetry.PayoutTags.Outcome, outcome);
        if (failed)
            activity?.SetStatus(ActivityStatusCode.Error, outcome);   // the description is the fixed outcome word, never a message

        metrics.PayoutProcessed(outcome);
    }
}
