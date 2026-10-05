using RondiTrack.Api.Domain;
namespace RondiTrack.Api.Services;

public interface IPayoutService
{
    Task<Result<Payout>> ProcessNextPayoutAsync(Guid stokvelId, Guid contributionCycleId, CancellationToken ct = default);
}