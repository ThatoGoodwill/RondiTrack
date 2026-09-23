namespace RondiTrack.Api.Domain;
public sealed class Contribution
{
    public const int MaxCycleLength = 40;
    public const decimal MaxAmount = 1_000_000m;
    private Contribution(Guid id, Guid stokvelId, Guid userId, string cycle, decimal amount, DateTimeOffset recordedAt)
    {
        Id = id; StokvelId = stokvelId; UserId = userId; Cycle = cycle; Amount = amount; RecordedAt = recordedAt;
    }
    public Guid Id { get; }
    public Guid StokvelId { get; }
    public Guid UserId { get; }
    public string Cycle { get; }              // e.g. "2026-09" for a monthly stokvel's September cycle
    public decimal Amount { get; }            // ZAR, decimal -- same reasoning as Stokvel.ContributionAmount
    public DateTimeOffset RecordedAt { get; }
    public static Result<Contribution> Create(Guid stokvelId, Guid userId, string? cycle, decimal amount)
    {
        if (stokvelId == Guid.Empty)
            return Result<Contribution>.Failure(Invalid("contribution.stokvel_required", "A stokvel id is required."));
        if (userId == Guid.Empty)
            return Result<Contribution>.Failure(Invalid("contribution.user_required", "A user id is required."));
        if (string.IsNullOrWhiteSpace(cycle))
            return Result<Contribution>.Failure(Invalid("contribution.cycle_required", "A cycle is required."));
        if (cycle.Trim().Length > MaxCycleLength)
            return Result<Contribution>.Failure(Invalid("contribution.cycle_too_long",
                $"Cycle text cannot exceed {MaxCycleLength} characters."));
        if (amount <= 0)
            return Result<Contribution>.Failure(Invalid("contribution.amount_not_positive", "Amount must be greater than zero."));
        if (decimal.Round(amount, 2) != amount)
            return Result<Contribution>.Failure(Invalid("contribution.amount_precision", "Amount cannot have more than 2 decimal places."));
        if (amount > MaxAmount)
            return Result<Contribution>.Failure(Invalid("contribution.amount_too_large", "Amount is unrealistically large."));
        return Result<Contribution>.Success(new Contribution(
            Guid.CreateVersion7(), stokvelId, userId, cycle.Trim(), amount, DateTimeOffset.UtcNow));
    }
    private static Error Invalid(string code, string message) => new(ErrorType.Validation, code, message);
}
