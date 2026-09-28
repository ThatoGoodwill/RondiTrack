using FluentValidation;
using RondiTrack.Api.Contracts;

namespace RondiTrack.Api.Validators;

// SHAPE-ONLY rules for ContributionRequest.
// Not here (they need repositories, so they are exceptions from ContributionService):
//   - does the cycle exist?  - is the user a member?  - already paid for this cycle?
public sealed class ContributionRequestValidator : AbstractValidator<ContributionRequest>
{
    public ContributionRequestValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.ContributionCycleId).NotEmpty();
        RuleFor(x => x.Amount)
            .GreaterThan(0)
            .LessThanOrEqualTo(1_000_000m)
            .PrecisionScale(18, 2, ignoreTrailingZeros: true)
            .WithMessage("Amount cannot have more than 2 decimal places.");
    }
}
