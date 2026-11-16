namespace RondiTrack.Api.Contracts;

/// <summary>The API owns the payout order, financial amounts and next member.</summary>
public sealed record PayoutOrderEntryResponse(
    Guid MemberId, string MemberName, int Position,
    long AmountCents, string Status, DateTimeOffset? PaidOnUtc);

public sealed record PayoutOrderResponse(
    Guid StokvelId, Guid CycleId, Guid? NextMemberId,
    IReadOnlyList<PayoutOrderEntryResponse> Entries);
