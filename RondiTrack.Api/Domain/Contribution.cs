namespace RondiTrack.Api.Domain;

// ---------------------------------------------------------------------------------
// ONE payment record: "this member paid this amount for this contribution cycle".
//
// CHANGED IN 4.3: it no longer stores a free-text "Cycle" string. It now points at a
// REAL ContributionCycle through ContributionCycleId. A typo like "2026-9" instead of
// "2026-09" can no longer sneak past the duplicate-payment check.
//
// Same rich-entity pattern as User/Stokvel: private constructor + Create() that
// validates first, so an invalid Contribution cannot exist in memory.
// ---------------------------------------------------------------------------------
public sealed class Contribution
{
    public const decimal MaxAmount = 1_000_000m;   // sanity ceiling (typo guard), in ZAR

    private Contribution(Guid id, Guid stokvelId, Guid userId, Guid contributionCycleId,
                         decimal amount, DateTimeOffset recordedAt)
    {
        Id = id;
        StokvelId = stokvelId;
        UserId = userId;
        ContributionCycleId = contributionCycleId;
        Amount = amount;
        RecordedAt = recordedAt;
    }

    public Guid Id { get; }
    public Guid StokvelId { get; }
    public Guid UserId { get; }
    public User? User { get; private set; }
    public Guid ContributionCycleId { get; }
    public decimal Amount { get; }               // decimal, never double: this is money
    public DateTimeOffset RecordedAt { get; }

    public static Result<Contribution> Create(Guid stokvelId, Guid userId, Guid contributionCycleId, decimal amount)
    {
        if (stokvelId == Guid.Empty)
            return Fail("contribution.stokvel_required", "A stokvel id is required.");
        if (userId == Guid.Empty)
            return Fail("contribution.user_required", "A user id is required.");
        if (contributionCycleId == Guid.Empty)
            return Fail("contribution.cycle_required", "A contribution cycle id is required.");
        if (amount <= 0)
            return Fail("contribution.amount_not_positive", "Amount must be greater than zero.");
        if (decimal.Round(amount, 2) != amount)
            return Fail("contribution.amount_precision", "Amount cannot have more than 2 decimal places.");
        if (amount > MaxAmount)
            return Fail("contribution.amount_too_large", "Amount is unrealistically large.");

        return Result<Contribution>.Success(new Contribution(
            Guid.CreateVersion7(), stokvelId, userId, contributionCycleId, amount, DateTimeOffset.UtcNow));
    }

    private static Result<Contribution> Fail(string code, string message) =>
        Result<Contribution>.Failure(new Error(ErrorType.Validation, code, message)); 
}
