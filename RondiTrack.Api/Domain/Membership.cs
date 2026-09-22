namespace RondiTrack.Api.Domain;
public sealed record Membership(Guid UserId, DateTimeOffset JoinedAt);