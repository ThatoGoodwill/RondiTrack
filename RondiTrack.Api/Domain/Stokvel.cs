namespace RondiTrack.Api.Domain;

public sealed class Stokvel
{
    public const int MinAllowedMembers = 2;
    public const int MaxAllowedMembers = 50;
    public const decimal MaxContribution = 1_000_000m;

    private readonly List<Membership> _members = [];
    private readonly Lock _sync = new();

    private Stokvel(Guid id, string name, decimal amount, ContributionFrequency freq, int max, DateTimeOffset createdAt)
    { Id = id; Name = name; ContributionAmount = amount; Frequency = freq; MaxMembers = max; CreatedAt = createdAt; }

    public Guid Id { get; }
    public string Name { get; private set; }
    public decimal ContributionAmount { get; private set; }
    public ContributionFrequency Frequency { get; private set; }
    public int MaxMembers { get; private set; }
    public DateTimeOffset CreatedAt { get; }
    public IReadOnlyList<Membership> Members { get { lock (_sync) { return _members.ToArray(); } } }

    public int MemberCount
{
    get { lock (_sync) { return _members.Count; } }
}

    public static Result<Stokvel> Create(string? name, decimal amount, ContributionFrequency freq, int max)
    {
        var error = Validate(name, amount, freq, max);
        if (error is not null) return Result<Stokvel>.Failure(error);
        return Result<Stokvel>.Success(new Stokvel(Guid.CreateVersion7(), name!.Trim(), amount, freq, max, DateTimeOffset.UtcNow));
    }

    public Result Update(string? name, decimal amount, ContributionFrequency freq, int max)
    {
        var error = Validate(name, amount, freq, max);
        if (error is not null) return Result.Failure(error);
        lock (_sync)
        {
            if (max < _members.Count)
                return Result.Failure(new Error(ErrorType.Conflict, "stokvel.capacity_below_members",
                    $"Cannot set max to {max}: already has {_members.Count} members."));
            Name = name!.Trim(); ContributionAmount = amount; Frequency = freq; MaxMembers = max;
            return Result.Success();
        }
    }

    public Result AddMember(Guid userId)
    {
        lock (_sync)
        {
            if (_members.Any(m => m.UserId == userId))
                return Result.Failure(new Error(ErrorType.Conflict, "membership.duplicate", "Already a member."));
            if (_members.Count >= MaxMembers)
                return Result.Failure(new Error(ErrorType.Conflict, "stokvel.full", $"Reached max of {MaxMembers} members."));
            _members.Add(new Membership(userId, DateTimeOffset.UtcNow));
            return Result.Success();
        }
    }

    public Result RemoveMember(Guid userId)
    {
        lock (_sync)
        {
            return _members.RemoveAll(m => m.UserId == userId) == 0
                ? Result.Failure(new Error(ErrorType.NotFound, "membership.not_found", "Not a member."))
                : Result.Success();
        }
    }

    public Membership? GetMember(Guid userId) { lock (_sync) { return _members.FirstOrDefault(m => m.UserId == userId); } }
    public bool HasMember(Guid userId) => GetMember(userId) is not null;

    private static Error? Validate(string? name, decimal amount, ContributionFrequency freq, int max)
    {
        if (string.IsNullOrWhiteSpace(name)) return Invalid("stokvel.name_required", "Name is required.");
        if (amount <= 0) return Invalid("stokvel.contribution_not_positive", "Amount must be > 0.");
        if (decimal.Round(amount, 2) != amount) return Invalid("stokvel.contribution_precision", "Max 2 decimal places.");
        if (amount > MaxContribution) return Invalid("stokvel.contribution_too_large", "Amount too large.");
        if (!Enum.IsDefined(freq)) return Invalid("stokvel.frequency_invalid", "Invalid frequency.");
        if (max < MinAllowedMembers || max > MaxAllowedMembers) return Invalid("stokvel.max_members_range", "Members must be 2-50.");
        return null;
    }

    private static Error Invalid(string code, string message) => new(ErrorType.Validation, code, message);
}