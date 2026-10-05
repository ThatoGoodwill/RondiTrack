namespace RondiTrack.Api.Domain;
// A record of one member receiving the pot for one contribution cycle.
// Kept deliberately minimal per the brief: no scheduling, no notifications, no partial payouts.
public sealed class Payout
{
   private Payout(Guid id, Guid stokvelId, Guid contributionCycleId, Guid recipientUserId,
   decimal amount, DateTimeOffset processedAt)
   {
      Id = id;
      StokvelId = stokvelId;
      ContributionCycleId = contributionCycleId;
      RecipientUserId = recipientUserId;
      Amount = amount;
      ProcessedAt = processedAt;
 }
     public Guid Id { get; }
     public Guid StokvelId { get; }
     public Guid ContributionCycleId { get; }
     public Guid RecipientUserId { get; }
     public decimal Amount { get; }
     public DateTimeOffset ProcessedAt { get; }
     public static Result<Payout> Create(Guid stokvelId, Guid contributionCycleId, Guid recipientUserId, decimal amount)
  {
    if (stokvelId == Guid.Empty)
    return Result<Payout>.Failure(Invalid("payout.stokvel_required", "A stokvel id is required."));
    if (contributionCycleId == Guid.Empty)
    return Result<Payout>.Failure(Invalid("payout.cycle_required", "A contribution cycle id is required."));
   if (recipientUserId == Guid.Empty)
    return Result<Payout>.Failure(Invalid("payout.recipient_required", "A recipient user id is required."));
   if (amount <= 0)
   return Result<Payout>.Failure(Invalid("payout.amount_not_positive", "Amount must be greater than zero."));
   return Result<Payout>.Success(new Payout(
   Guid.CreateVersion7(), stokvelId, contributionCycleId, recipientUserId, amount, DateTimeOffset.UtcNow));
 }
 private static Error Invalid(string code, string message) => new(ErrorType.Validation, code, message);
}