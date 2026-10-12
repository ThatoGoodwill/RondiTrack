namespace RondiTrack.Domain.Exceptions;

// "We know who you are, and you may not do this."  --> the handler answers 403.
public class ForbiddenException : RondiTrackException
{
    public ForbiddenException(string code, string message) : base(code, message) { }
}
