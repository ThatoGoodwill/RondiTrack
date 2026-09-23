namespace RondiTrack.Api.Contracts;

public sealed record UserRequest(string? FirstName, string? LastName, string? Email, DateOnly DateOfBirth);