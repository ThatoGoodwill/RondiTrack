namespace RondiTrack.Api.Contracts;

public sealed record PageRequest(int? PageSize, string? PageToken, string? Sort, string? Filter);