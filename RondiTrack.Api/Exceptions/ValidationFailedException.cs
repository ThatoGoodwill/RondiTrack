namespace RondiTrack.Api.Exceptions;
// A safety net: an entity's own Validate() rejected something that FluentValidation should
// already have caught. Reaching this is rare by design -- it means defence-in-depth worked. -> 400
public sealed class ValidationFailedException(string code, string message) : RondiTrackException(code, message);