using RondiTrack.Api.Domain;
namespace RondiTrack.Api.Contracts;

public sealed record StokvelRequest(string? Name, decimal ContributionAmount, ContributionFrequency Frequency, int MaxMembers);