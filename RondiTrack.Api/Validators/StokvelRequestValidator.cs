using FluentValidation;
using RondiTrack.Api.Contracts;

namespace RondiTrack.Api.Validators;

// SHAPE-ONLY rules for StokvelRequest.
public sealed class StokvelRequestValidator : AbstractValidator<StokvelRequest>
{
    public StokvelRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);

        RuleFor(x => x.ContributionAmount)
            .GreaterThan(0)
            .LessThanOrEqualTo(1_000_000m)
            .PrecisionScale(18, 2, ignoreTrailingZeros: true)   // money: at most 2 decimal places
            .WithMessage("Contribution amount cannot have more than 2 decimal places.");

        RuleFor(x => x.Frequency).IsInEnum();                   // Weekly / Fortnightly / Monthly only
        RuleFor(x => x.MaxMembers).InclusiveBetween(2, 50);
    }
}
