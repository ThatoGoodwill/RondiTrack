namespace RondiTrack.Api.Exceptions;
// A well-formed request refers to something that doesn't exist or doesn't apply. -> 422
public sealed class UnprocessableException(string code, string message) : RondiTrackException(code, message);