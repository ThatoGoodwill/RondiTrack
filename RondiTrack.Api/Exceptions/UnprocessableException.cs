namespace RondiTrack.Api.Exceptions;

// ---------------------------------------------------------------------------------
// The request is well-formed and understood, but it POINTS AT something that does
// not exist or does not apply.                           --> handler answers 422
// Examples: POST a member with a userId that is not a real user;
//           record a contribution for a user who is not a member of the stokvel;
//           reference a contribution cycle that does not exist for this stokvel.
//
// 404 vs 422:  404 = the resource in the URL is missing.
//              422 = something referenced INSIDE the body is missing.
// ---------------------------------------------------------------------------------
public sealed class UnprocessableException : RondiTrackException
{
    public UnprocessableException(string code, string message) : base(code, message) { }
}
