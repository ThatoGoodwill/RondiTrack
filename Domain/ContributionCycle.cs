namespace RondiTrack.Domain;

// A specific collection period for ONE stokvel, e.g. Label "2026-09", TargetAmount R500 per member.
// 6.4: it can have an end date (a DATE in the stokvel's own calendar, Africa/Johannesburg, inclusive).
// The cycle-close job closes it at 00:00 the next day, exactly once.
public sealed class ContributionCycle
{
    private readonly object _sync = new();
    private readonly List<Contribution> _contributions = [];

    public const int MaxLabelLength = 40;
    public const decimal MaxTargetAmount = 1_000_000m;

    public IReadOnlyList<Contribution> Contributions
    {
        get { lock (_sync) { return _contributions.ToArray(); } }
    }

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
    public string Label { get; private set; }
    public decimal TargetAmount { get; private set; }   // expected from each member (ZAR)
    public DateTimeOffset CreatedAt { get; }

    public DateOnly? EndsOn { get; private set; }       // last day of the cycle, SAST; null = never closed by the job
    public DateTimeOffset? ClosedAt { get; private set; }

    public ContributionCycleStatus Status { get; private set; } = ContributionCycleStatus.Open;
    public void MarkPaidOut() => Status = ContributionCycleStatus.PaidOut;

    // createdAt: the cycle-close job passes the injected clock's "now". Everyone else leaves it null (real time).
    public static Result<ContributionCycle> Create(Guid stokvelId, string? label, decimal targetAmount,
        DateOnly? endsOn = null, DateTimeOffset? createdAt = null)
    {
        var error = Validate(stokvelId, label, targetAmount);
        if (error is not null) return Result<ContributionCycle>.Failure(error);

        var cycle = new ContributionCycle(
            Guid.CreateVersion7(), stokvelId, label!.Trim(), targetAmount, createdAt ?? DateTimeOffset.UtcNow);
        cycle.EndsOn = endsOn;
        return Result<ContributionCycle>.Success(cycle);
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