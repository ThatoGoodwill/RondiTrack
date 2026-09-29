namespace RondiTrack.Api.Domain;

// "This user joined this stokvel at this moment." Immutable: you never edit a membership,
// you either have one or you don't. Lives INSIDE a Stokvel.
public sealed record Membership(Guid UserId, DateTimeOffset JoinedAt);
