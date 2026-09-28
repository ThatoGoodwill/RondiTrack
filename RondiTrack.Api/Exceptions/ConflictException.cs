namespace RondiTrack.Api.Exceptions;

// ---------------------------------------------------------------------------------
// The request is fine on its own, but the CURRENT STATE of the system makes it
// impossible right now.                                  --> handler answers 409
// Examples: adding a member who is already a member, a full stokvel,
//           a contribution already recorded for that cycle, an email already taken.
//
// NOTE: this class is deliberately NOT sealed. IdempotencyKeyConflictException
// inherits from it, so the handler's "any ConflictException -> 409" rule also
// catches that child automatically (inheritance does the work, no extra code).
// ---------------------------------------------------------------------------------
public class ConflictException : RondiTrackException
{
    public ConflictException(string code, string message) : base(code, message) { }
}
