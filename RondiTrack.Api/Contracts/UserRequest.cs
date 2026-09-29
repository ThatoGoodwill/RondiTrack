namespace RondiTrack.Api.Contracts;

// INCOMING shape for creating/replacing a user. Pure data, no logic, no attributes.
// Nullable strings on purpose: the VALIDATOR rejects blanks, not the framework.
// Only these four fields can ever arrive: there is no blank on the form for anything else
// (that is what protects against "over-posting").
public sealed record UserRequest(string? FirstName, string? LastName, string? Email, DateOnly DateOfBirth);
