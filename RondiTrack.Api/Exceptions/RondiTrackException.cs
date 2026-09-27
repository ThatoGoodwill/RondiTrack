using RondiTrack.Api.Exceptions;

// The common base every RondiTrack failure shares. Never thrown directly -- its always one of its subtypes.

public abstract class RondiTrackException : Exception
{
    public string Code { get; }

    protected RondiTrackException(string code, string message) : base(message)
    {
        Code = code;
    }   
}