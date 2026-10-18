using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RondiTrack.Api.Observability;
using RondiTrack.Infrastructure.Data;

namespace RondiTrack.Api.Jobs;

// Environment-variable form: Housekeeping__Enabled, __IntervalMinutes, __RefreshTokenRetentionDays, __IdempotencyRetentionDays.
public sealed class HousekeepingOptions
{
    public const string SectionName = "Housekeeping";

    public bool Enabled { get; init; } = true;
    public int IntervalMinutes { get; init; } = 60;
    public int RefreshTokenRetentionDays { get; init; } = 30;   // N: days AFTER a refresh token expired
    public int IdempotencyRetentionDays { get; init; } = 7;     // M: days after the key was first seen

    public void EnsureValid()
    {
        if (IntervalMinutes < 1) throw new InvalidOperationException("Housekeeping__IntervalMinutes must be 1 or more.");
        if (RefreshTokenRetentionDays < 1) throw new InvalidOperationException("Housekeeping__RefreshTokenRetentionDays must be 1 or more.");
        if (IdempotencyRetentionDays < 1) throw new InvalidOperationException("Housekeeping__IdempotencyRetentionDays must be 1 or more.");
    }
}

public sealed record HousekeepingResult(int RefreshTokensDeleted, int IdempotencyKeysDeleted);

// Deletes old evidence with set-based DELETE statements (nothing is loaded into memory).
// Safe to run twice or on two replicas at once: deleting rows that are already gone changes nothing.
public sealed class HousekeepingJob(
    IServiceScopeFactory scopeFactory,
    TimeProvider clock,
    IOptions<HousekeepingOptions> options,
    JobMetrics metrics,
    ILogger<HousekeepingJob> logger) : BackgroundService
{
    public const string SpanName = "housekeeping.run";
    private readonly HousekeepingOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            logger.LogInformation("Housekeeping job is disabled (Housekeeping__Enabled=false).");
            return;
        }

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(_options.IntervalMinutes), clock);
        try
        {
            do { await RunOnceAsync(stoppingToken); }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) { /* shutting down */ }
    }

    public async Task<HousekeepingResult> RunOnceAsync(CancellationToken ct = default)
    {
        using var activity = RondiTrackTelemetry.ActivitySource.StartActivity(SpanName);
        try
        {
            var now = clock.GetUtcNow().ToUniversalTime();
            var tokenCutoff = now.UtcDateTime.AddDays(-_options.RefreshTokenRetentionDays);   // expired MORE than N days ago
            var keyCutoff = now.AddDays(-_options.IdempotencyRetentionDays);                  // first seen MORE than M days ago

            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<RondiTrackDbContext>();

            var tokens = await db.RefreshTokens.Where(t => t.ExpiresAt < tokenCutoff).ExecuteDeleteAsync(ct);
            var keys = await db.IdempotencyKeys.Where(k => k.CreatedAt < keyCutoff).ExecuteDeleteAsync(ct);

            metrics.Deleted(HousekeepingKinds.RefreshTokens, tokens);
            metrics.Deleted(HousekeepingKinds.IdempotencyKeys, keys);
            activity?.SetTag("ronditrack.job.refresh_tokens_deleted", tokens);
            activity?.SetTag("ronditrack.job.idempotency_keys_deleted", keys);
            logger.LogInformation("Housekeeping finished. RefreshTokensDeleted={Tokens} IdempotencyKeysDeleted={Keys}", tokens, keys);
            return new HousekeepingResult(tokens, keys);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error);
            logger.LogError(ex, "Housekeeping run failed; it is retried on the next run.");
            return new HousekeepingResult(0, 0);
        }
    }
}
