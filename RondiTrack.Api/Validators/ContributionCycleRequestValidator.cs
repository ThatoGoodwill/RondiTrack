using FluentValidation;
using RondiTrack.Api.Contracts;

namespace RondiTrack.Api.Validators;

// SHAPE-ONLY rules for ContributionCycleRequest.
public sealed class ContributionCycleRequestValidator : AbstractValidator<ContributionCycleRequest>
{
    public ContributionCycleRequestValidator()
    {
        RuleFor(x => x.Label).NotEmpty().MaximumLength(40);
        RuleFor(x => x.TargetAmount)
            .GreaterThan(0)
            .LessThanOrEqualTo(1_000_000m)
            .PrecisionScale(18, 2, ignoreTrailingZeros: true)
            .WithMessage("Target amount cannot have more than 2 decimal places.");
    }
}
