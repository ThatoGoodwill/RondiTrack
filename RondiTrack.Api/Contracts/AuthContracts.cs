using RondiTrack.Domain;

namespace RondiTrack.Api.Contracts;

// Nullable strings on purpose: the VALIDATOR rejects blanks, not the framework.
public sealed record RegisterRequest(
    string? FirstName, string? LastName, string? Email, DateOnly DateOfBirth, string? Password);

public sealed record LoginRequest(string? Email, string? Password);

// The access token goes in the BODY (the frontend keeps it in memory).
// The refresh token is NOT here: it travels only in an HttpOnly cookie.
public sealed record TokenResponse(string AccessToken, DateTime ExpiresAt);
