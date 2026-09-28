namespace RondiTrack.Api.Exceptions;

// ---------------------------------------------------------------------------------
// The thing the request is ABOUT does not exist.        --> handler answers 404
// Example: GET /api/users/{id} where no user has that id.
// ---------------------------------------------------------------------------------
public class NotFoundException : RondiTrackException
{
    public NotFoundException(string code, string message) : base(code, message) { }
}
