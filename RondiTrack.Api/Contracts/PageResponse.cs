namespace RondiTrack.Api.Contracts;

public sealed record PageResponse<T>(IReadOnlyList<T> Items, string? NextPageToken);