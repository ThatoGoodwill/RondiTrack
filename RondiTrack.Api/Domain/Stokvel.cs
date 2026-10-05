namespace RondiTrack.Api.Domain;

// ---------------------------------------------------------------------------------
// A savings group. RICH entity, and the OWNER of the membership rules:
//   - no duplicate members            - never more than MaxMembers
//   - contribution > 0, max 2 decimals (money -> decimal, never double)
//   - name required, frequency defined, max members between 2 and 50
//   - MaxMembers can never be lowered below the current member count
// The member list is PRIVATE; outsiders only get a read-only SNAPSHOT copy.
// ---------------------------------------------------------------------------------
public sealed class Stokvel
{
    public const int MaxNameLength = 100;
    public const int MinAllowedMembers = 2;
    public const int MaxAllowedMembers = 50;
    public const decimal MaxContribution = 1_000_000m;   // sanity ceiling (typo guard), in ZAR

    private readonly List<StokvelMember>_members = [];
    private readonly Lock _sync = new();                  // requests run in parallel: lock around list changes

    private Stokvel(Guid id, string name, decimal contributionAmount,
                    ContributionFrequency frequency, int maxMembers, DateTimeOffset createdAt)
    {
        Id = id;
        Name = name;
        ContributionAmount = contributionAmount;
        Frequency = frequency;
        MaxMembers = maxMembers;
        CreatedAt = createdAt;
    }

    public Guid Id { get; }
    public string Name { get; private set; }
    public decimal ContributionAmount { get; private set; }
    public ContributionFrequency Frequency { get; private set; }
    public int MaxMembers { get; private set; }
    public DateTimeOffset CreatedAt { get; }

    // A SNAPSHOT copy: callers can neither modify our list nor be broken by concurrent changes.
    public IReadOnlyList<StokvelMember> Members
    {
        get { lock (_sync) { return _members.ToArray(); } }
    }

    public int MemberCount
    {
        get { lock (_sync) { return _members.Count; } }
    }

    public static Result<Stokvel> Create(string? name, decimal contributionAmount,
                                         ContributionFrequency frequency, int maxMembers)
    {
        var error = Validate(name, contributionAmount, frequency, maxMembers);
        if (error is not null) return Result<Stokvel>.Failure(error);

        return Result<Stokvel>.Success(new Stokvel(
            Guid.CreateVersion7(), name!.Trim(), contributionAmount, frequency, maxMembers, DateTimeOffset.UtcNow));
    }

    public Result Update(string? name, decimal contributionAmount,
                         ContributionFrequency frequency, int maxMembers)
    {
        var error = Validate(name, contributionAmount, frequency, maxMembers);
        if (error is not null) return Result.Failure(error);

        lock (_sync)
        {
            if (maxMembers < _members.Count)
                return Result.Failure(new Error(ErrorType.Conflict, "stokvel.capacity_below_members",
                    $"Cannot set the maximum to {maxMembers}: the stokvel already has {_members.Count} members."));

            Name = name!.Trim();
            ContributionAmount = contributionAmount;
            Frequency = frequency;
            MaxMembers = maxMembers;
            return Result.Success();
        }
    }

    // ---------------- the relationship rules ----------------
    public Result AddMember(Guid userId)
    {
        if (userId == Guid.Empty)
            return Result.Failure(new Error(ErrorType.Validation, "membership.user_id_required",
                "A valid user id is required."));

        // check-then-add must be ONE atomic step, otherwise two requests could both pass the check
        lock (_sync)
        {
            if (_members.Any(m => m.UserId == userId))
                return Result.Failure(new Error(ErrorType.Conflict, "membership.duplicate",
                    "This user is already a member of the stokvel."));

            if (_members.Count >= MaxMembers)
                return Result.Failure(new Error(ErrorType.Conflict, "stokvel.full",
                    $"This stokvel has reached its maximum of {MaxMembers} members."));

            _members.Add(StokvelMember.Create(Id, userId));
            return Result.Success();
        }
    }

    public Result RemoveMember(Guid userId)
    {
        lock (_sync)
        {
            var removed = _members.RemoveAll(m => m.UserId == userId);
            return removed == 0
                ? Result.Failure(new Error(ErrorType.NotFound, "membership.not_found",
                    "This user is not a member of the stokvel."))
                : Result.Success();
        }
    }

    public StokvelMember? GetMember(Guid userId)
    {
        lock (_sync) { return _members.FirstOrDefault(m => m.UserId == userId); }
    }

    public bool HasMember(Guid userId) => GetMember(userId) is not null;

    // ---------------- the field rules ----------------
    private static Error? Validate(string? name, decimal contributionAmount,
                                   ContributionFrequency frequency, int maxMembers)
    {
        if (string.IsNullOrWhiteSpace(name))
            return Invalid("stokvel.name_required", "Stokvel name is required.");
        if (name.Trim().Length > MaxNameLength)
            return Invalid("stokvel.name_too_long", $"Name cannot exceed {MaxNameLength} characters.");

        if (contributionAmount <= 0)
            return Invalid("stokvel.contribution_not_positive", "Contribution amount must be greater than zero.");
        if (decimal.Round(contributionAmount, 2) != contributionAmount)
            return Invalid("stokvel.contribution_precision", "Contribution amount cannot have more than 2 decimal places.");
        if (contributionAmount > MaxContribution)
            return Invalid("stokvel.contribution_too_large", $"Contribution amount cannot exceed R{MaxContribution:N2}.");

        if (!Enum.IsDefined(frequency))
            return Invalid("stokvel.frequency_invalid", "Frequency must be Weekly, Fortnightly or Monthly.");

        if (maxMembers < MinAllowedMembers || maxMembers > MaxAllowedMembers)
            return Invalid("stokvel.max_members_range",
                $"Maximum members must be between {MinAllowedMembers} and {MaxAllowedMembers}.");

        return null;
    }

    private static Error Invalid(string code, string message) => new(ErrorType.Validation, code, message);
}
