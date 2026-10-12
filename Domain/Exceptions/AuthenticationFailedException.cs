namespace RondiTrack.Domain.Exceptions;

// "We do not know who you are."  --> the handler answers 401.
// Thrown for a wrong password, an unknown email, or a refresh token we will not accept.
public class AuthenticationFailedException : RondiTrackException
{
    public AuthenticationFailedException(string code, string message) : base(code, message) { }
}
