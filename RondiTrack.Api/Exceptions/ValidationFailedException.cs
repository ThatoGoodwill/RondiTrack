namespace RondiTrack.Api.Exceptions;

// ---------------------------------------------------------------------------------
// A value is invalid on its own, regardless of anything else.   --> handler answers 400
//
// FluentValidation catches most of these BEFORE the service layer runs (it throws
// FluentValidation's own ValidationException, which the handler also maps to 400).
// This class is the SAFETY NET (defence in depth):
//   - used when an entity's own Create()/Update() rejects a value, and
//   - used by ValidationFilter for malformed / missing request bodies.
// ---------------------------------------------------------------------------------
public sealed class ValidationFailedException : RondiTrackException
{
    public ValidationFailedException(string code, string message) : base(code, message) { }
}
