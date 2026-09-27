namespace RondiTrack.Api.Exceptions;

    // The request is fine on its own, but theCURRENT STATE of the system makes it impossible. -> 409
    // Not seale -- IempontencyKeyConflictException below deliberately extends it.
    public class ConflictException(string code, string message) : RondiTrackException(code, message);
 
 