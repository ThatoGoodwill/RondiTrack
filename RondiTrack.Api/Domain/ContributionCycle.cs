namespace RondiTrack.Api.Domain;

// ---------------------------------------------------------------------------------
// NEW IN 4.3: a specific collection period for ONE stokvel.
//   e.g. Label "2026-09", TargetAmount R500  ->  "September 2026, R500 per member".
//
// It only protects its OWN fields (label + amount). Whether the parent stokvel
// exists is a cross-entity question, so that check lives in the controller.
// ---------------------------------------------------------------------------------
public sealed class ContributionCycle
{
    public const int MaxLabelLength = 40;
    public const decimal MaxTargetAmount = 1_000_000m;

    private ContributionCycle(Guid id, Guid stokvelId, string label, decimal targetAmount, DateTimeOffset createdAt)
    {
        Id = id;
        StokvelId = stokvelId;
        Label = label;
        TargetAmount = targetAmount;
        CreatedAt = createdAt;
    }

    public Guid Id { get; }
    public Guid StokvelId { get; }                      // never changes after creation
    public string Label { get; private set; }           // identifies the period, e.g. "2026-09"
    public decimal TargetAmount { get; private set; }   // amount expected from each member (ZAR)
    public DateTimeOffset CreatedAt { get; }

    public ContributionCycleStatus Status { get; private set; } = ContributionCycleStatus.Open;
    public void MarkPaidOut() => Status = ContributionCycleStatus.PaidOut;

    public static Result<ContributionCycle> Create(Guid stokvelId, string? label, decimal targetAmount)
    {
        var error = Validate(stokvelId, label, targetAmount);
        if (error is not null) return Result<ContributionCycle>.Failure(error);

        return Result<ContributionCycle>.Success(new ContributionCycle(
            Guid.CreateVersion7(), stokvelId, label!.Trim(), targetAmount, DateTimeOffset.UtcNow));
    }

    // Validate EVERYTHING first, then assign: a failed update leaves the cycle unchanged.
    public Result Update(string? label, decimal targetAmount)
    {
        var error = Validate(StokvelId, label, targetAmount);
        if (error is not null) return Result.Failure(error);

        Label = label!.Trim();
        TargetAmount = targetAmount;
        return Result.Success();
    }

    private static Error? Validate(Guid stokvelId, string? label, decimal targetAmount)
    {
        if (stokvelId == Guid.Empty)
            return Invalid("cycle.stokvel_required", "A stokvel id is required.");
        if (string.IsNullOrWhiteSpace(label))
            return Invalid("cycle.label_required", "A label is required.");
        if (label.Trim().Length > MaxLabelLength)
            return Invalid("cycle.label_too_long", $"Label cannot exceed {MaxLabelLength} characters.");
        if (targetAmount <= 0)
            return Invalid("cycle.target_not_positive", "Target amount must be greater than zero.");
        if (decimal.Round(targetAmount, 2) != targetAmount)
            return Invalid("cycle.target_precision", "Target amount cannot have more than 2 decimal places.");
        if (targetAmount > MaxTargetAmount)
            return Invalid("cycle.target_too_large", "Target amount is unrealistically large.");
        return null;   // null = no error
    }

    private static Error Invalid(string code, string message) => new(ErrorType.Validation, code, message);
}
