namespace RondiTrack.Api.Exceptions;

// ---------------------------------------------------------------------------------
// THE BASE OF THE EXCEPTION HIERARCHY.
// Every "legitimate" way a RondiTrack request can fail is a child of this class.
// It is abstract: you never throw a RondiTrackException directly, you throw one of
// its children (NotFoundException, ConflictException, ...).
//
// "Code"    = a stable, machine-readable label, e.g. "stokvel.not_found".
// "Message" = the human-readable explanation (comes from Exception itself).
//
// Why a shared base? So the centralized handler can say "is this one of OUR
// exceptions?" with one type check, and so every failure always carries a Code.
// ---------------------------------------------------------------------------------
public abstract class RondiTrackException : Exception
{
    public string Code { get; }

    protected RondiTrackException(string code, string message) : base(message)
    {
        Code = code;
    }
}
