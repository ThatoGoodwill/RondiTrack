using Microsoft.EntityFrameworkCore;
using RondiTrack.Domain;
using RondiTrack.Infrastructure.Data;

namespace RondiTrack.Api.Services;

// Rules (from the controller's own description): the next eligible recipient is the earliest-joined member
// who has not yet received a payout in this stokvel; the pot is the sum of this cycle's contributions;
// the Payout row and the cycle's "paid out" status are saved together in ONE SaveChangesAsync (one transaction).
public sealed class PayoutService(RondiTrackDbContext context) : IPayoutService
{
    public async Task<Result<Payout>> ProcessNextPayoutAsync(
        Guid stokvelId, Guid contributionCycleId, CancellationToken ct = default)
    {
        var cycle = await context.ContributionCycles
            .FirstOrDefaultAsync(c => c.Id == contributionCycleId && c.StokvelId == stokvelId, ct);
        if (cycle is null)
            return Result<Payout>.Failure(new Error(ErrorType.NotFound, "cycle.not_found",
                $"No contribution cycle with id {contributionCycleId} exists for this stokvel."));

        if (cycle.Status == ContributionCycleStatus.PaidOut)
            return Result<Payout>.Failure(new Error(ErrorType.Conflict, "payout.already_processed",
                "This cycle has already been paid out."));

        var alreadyPaid = await context.Payouts
            .Where(p => p.StokvelId == stokvelId)
            .Select(p => p.RecipientUserId)
            .ToListAsync(ct);

        var next = await context.StokvelMembers.AsNoTracking()
            .Where(m => m.StokvelId == stokvelId && !alreadyPaid.Contains(m.UserId))
            .OrderBy(m => m.JoinedAtUtc).ThenBy(m => m.UserId)
            .FirstOrDefaultAsync(ct);
        if (next is null)
            return Result<Payout>.Failure(new Error(ErrorType.Conflict, "payout.rotation_complete",
                "Every member of this stokvel has already received a payout."));

        var pot = await context.Contributions
            .Where(c => c.StokvelId == stokvelId && c.ContributionCycleId == contributionCycleId)
            .SumAsync(c => (decimal?)c.Amount, ct) ?? 0m;
        if (pot <= 0)
            return Result<Payout>.Failure(new Error(ErrorType.Conflict, "payout.no_contributions",
                "No contributions have been recorded for this cycle, so there is nothing to pay out."));

        var created = Payout.Create(stokvelId, contributionCycleId, next.UserId, pot);
        if (!created.IsSuccess) return created;

        cycle.MarkPaidOut();
        context.Payouts.Add(created.Value);
        await context.SaveChangesAsync(ct);   // both writes succeed or fail together

        return created;
    }
}