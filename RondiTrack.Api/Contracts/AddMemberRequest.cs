namespace RondiTrack.Api.Contracts;

// INCOMING shape for POST /api/stokvels/{id}/members : just the user to add.
public sealed record AddMemberRequest(Guid UserId);
