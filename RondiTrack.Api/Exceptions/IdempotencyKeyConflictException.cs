namespace RondiTrack.Api.Exceptions;

// ---------------------------------------------------------------------------------
// A MORE SPECIFIC kind of conflict: the client re-used an Idempotency-Key with a
// DIFFERENT request body.                                --> still 409 (it IS a ConflictException)
//
// Why is this a separate class from a duplicate contribution?
//   - A duplicate contribution is a BUSINESS-STATE conflict ("already paid").
//   - A reused key is a PROTOCOL misuse ("this key belongs to another request").
// Different meaning, same HTTP consequence -> model it as a SUBTYPE of
// ConflictException. We keep the distinction in code, and the handler needs
// zero extra lines to map it to 409.
// ---------------------------------------------------------------------------------
public sealed class IdempotencyKeyConflictException : ConflictException
{
    public IdempotencyKeyConflictException(string code, string message) : base(code, message) { }
}
