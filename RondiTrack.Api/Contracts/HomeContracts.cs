namespace RondiTrack.Api.Contracts;

// OUTGOING shape of the member home screen. Only what the screen shows.
public sealed record HomeResponse(IReadOnlyList<HomeStokvelResponse> Stokvels);

public sealed record HomeStokvelResponse(
    Guid StokvelId,                 // the screen links to the stokvel
    string Name,
    string Role,                    // "Member" | "Treasurer" | "Admin": a name, never a number
    HomeCycleResponse? CurrentCycle,// null when the stokvel has no open cycle
    DateTime? NextPayoutDueUtc);    // UTC; null when there is no open cycle

public sealed record HomeCycleResponse(
    Guid Id,
    string Label,                   // e.g. "2026-09"
    long PaidCents,                 // what THE CALLER has paid this cycle (never anyone else's amount)
    long OwedCents);                // what THE CALLER still owes (target minus paid, never below zero)