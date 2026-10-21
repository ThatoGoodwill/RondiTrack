namespace RondiTrack.Api.Contracts;

public sealed record MeResponse(Guid Id, string FirstName, string LastName, string Email);