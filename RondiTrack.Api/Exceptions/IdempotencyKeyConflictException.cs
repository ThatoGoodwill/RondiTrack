namespace RondiTrack.Api.Exceptions;

// A specific KIND of conflict: not a business-state clash, but a protocol-level misuse of a key.
// Still IS a ConflictException, so the handler's "any ConflictException -> 409" rule catches this too,
// with no extra code -- see the Concepts Guide, Chapter 4.2, for the full reasoning.
public sealed class IdempotencyKeyConflictException(string code, string message) : ConflictException(code, message);
