using FluentValidation;
using RondiTrack.Api.Contracts;

namespace RondiTrack.Api.Validators;

// SHAPE-ONLY rules for AddMemberRequest.
// NotEmpty() on a Guid means "not Guid.Empty" (all zeroes).
// Whether that user actually EXISTS is NOT checked here (needs a repository) ->
// MembershipService throws UnprocessableException for that.
public sealed class AddMemberRequestValidator : AbstractValidator<AddMemberRequest>
{
    public AddMemberRequestValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
    }
}
