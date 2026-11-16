using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RondiTrack.Api.Authorization;
using RondiTrack.Api.Contracts;
using RondiTrack.Api.Services;
using RondiTrack.Domain;
using RondiTrack.Domain.Exceptions;
using RondiTrack.Infrastructure.Data;
using Microsoft.AspNetCore.RateLimiting;
using RondiTrack.Api.Frontend;

namespace RondiTrack.Api.Controllers;

[ApiController]
[Route(ApiRoutes.Base + "/stokvels/{stokvelId:guid}/cycles/{cycleId:guid}/payout")]
public class PayoutsController(IPayoutService payoutService) : ApiControllerBase
{
    // Mobile 10.3: a read-only server-owned rotation. Never let Flutter work
    // out payment order, amount, statuses or next recipient itself.
    [HttpGet("order")]
    [StokvelAccess(Policies.StokvelMember)]
    [EndpointSummary("Get a cycle's rotation and payout order")]
    [ProducesResponseType<PayoutOrderResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PayoutOrderResponse>> GetOrder(
        Guid stokvelId, Guid cycleId, [FromServices] RondiTrackDbContext context, CancellationToken ct)
    {
        // The same 404 for nonexistent and non-member resources is enforced by
        // the StokvelAccess policy before querying data.
        var cycle = await context.ContributionCycles.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == cycleId && c.StokvelId == stokvelId, ct)
            ?? throw new NotFoundException("cycle.not_found", "Not found");

        var members = await context.StokvelMembers.AsNoTracking()
            .Where(m => m.StokvelId == stokvelId)
            .OrderBy(m => m.JoinedAtUtc).ThenBy(m => m.UserId)
            .ToListAsync(ct);
        var ids = members.Select(m => m.UserId).ToArray();
        var names = await context.Users.AsNoTracking()
            .Where(u => ids.Contains(u.Id))
            .Select(u => new { u.Id, u.FirstName, u.LastName })
            .ToDictionaryAsync(u => u.Id, u => u.FirstName + " " + u.LastName, ct);
        var allPayouts = await context.Payouts.AsNoTracking()
            .Where(p => p.StokvelId == stokvelId)
            .OrderBy(p => p.ProcessedAt)
            .ToListAsync(ct);
        var processedByMember = allPayouts
            .GroupBy(p => p.RecipientUserId)
            .ToDictionary(group => group.Key, group => group.First());
        var nextMemberId = members.Select(m => (Guid?)m.UserId)
            .FirstOrDefault(id => id.HasValue && !processedByMember.ContainsKey(id.Value));
        // This is a server decision and uses integer cents in the DTO.
        var pot = await context.Contributions.AsNoTracking()
            .Where(c => c.StokvelId == stokvelId && c.ContributionCycleId == cycle.Id)
            .SumAsync(c => (decimal?)c.Amount, ct) ?? 0m;
        var amountCents = decimal.ToInt64(decimal.Round(pot * 100m, 0));
        var entries = members.Select((member, index) =>
        {
            var hasPayout = processedByMember.TryGetValue(member.UserId, out var payout);
            var status = hasPayout ? "Paid" : nextMemberId == member.UserId ? "Next" : "Waiting";
            return new PayoutOrderEntryResponse(
                member.UserId, names.GetValueOrDefault(member.UserId, "Member"),
                index + 1, hasPayout ? decimal.ToInt64(decimal.Round(payout!.Amount * 100m, 0)) : amountCents,
                status, hasPayout ? payout!.ProcessedAt : null);
        }).ToList();
        return Ok(new PayoutOrderResponse(stokvelId, cycleId, nextMemberId, entries));
    }

    [HttpPost]
    [EnableRateLimiting(RateLimitPolicies.Payouts)]
    [StokvelAccess(Policies.StokvelTreasurer)]   // treasurer or chairperson only
    [EndpointSummary("Process the next payout for a stokvel's contribution cycle")]
    [EndpointDescription("Determines the next eligible recipient by join order, creates a Payout record, " +
                          "and marks the cycle as paid out -- both writes succeed or fail together.")]
    [ProducesResponseType<PayoutResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PayoutResponse>> Process(Guid stokvelId, Guid cycleId, CancellationToken ct)
    {
        var payout = (await payoutService.ProcessNextPayoutAsync(stokvelId, cycleId, ct)).ThrowIfFailure();
        return StatusCode(StatusCodes.Status201Created, PayoutResponse.FromEntity(payout));
    }

    [HttpGet(ApiRoutes.Root + "/payouts/{id:guid}")]
    [EndpointSummary("Get a payout by id")]
    [ProducesResponseType<PayoutResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PayoutResponse>> GetById(
        Guid id, [FromServices] RondiTrackDbContext context, CancellationToken ct)
    {
        var payout = await context.Payouts.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new NotFoundException("payout.not_found", $"No payout with id {id} exists.");

        // 6.1: this route has no {stokvelId}, so authorize against the payout's OWN stokvel.
        // A non-member gets the SAME payout.not_found as a payout that does not exist.
        await this.EnsureStokvelAccessAsync(payout.StokvelId, Policies.StokvelMember,
            "payout.not_found", $"No payout with id {id} exists.");

        Response.Headers.ETag = $"\"{payout.Version}\"";
        return Ok(PayoutResponse.FromEntity(payout));
    }

    [HttpPut(ApiRoutes.Root + "/payouts/{id:guid}")]
    [EndpointSummary("Update a payout's amount (optimistic concurrency)")]
    [ProducesResponseType<PayoutResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PayoutResponse>> UpdateAmount(
        Guid id,
        [FromBody] decimal amount,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        [FromServices] RondiTrackDbContext context,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(ifMatch) || !uint.TryParse(ifMatch.Trim('"'), out var clientVersion))
            throw new ValidationFailedException("payout.if_match_required",
                "A valid If-Match header (the ETag from a previous GET) is required.");

        var payout = await context.Payouts.FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new NotFoundException("payout.not_found", $"No payout with id {id} exists.");

        // 6.1: changing a payout needs treasurer or chairperson in the payout's stokvel.
        await this.EnsureStokvelAccessAsync(payout.StokvelId, Policies.StokvelTreasurer,
            "payout.not_found", $"No payout with id {id} exists.");

        // Only update if the row's xmin still equals what the client last saw.
        context.Entry(payout).Property(p => p.Version).OriginalValue = clientVersion;

        // Demo-only: Payout has no public update method.
        typeof(Payout).GetProperty(nameof(Payout.Amount))!.SetValue(payout, amount);

        await context.SaveChangesAsync(ct);   // stale token -> DbUpdateConcurrencyException -> central handler -> 409

        Response.Headers.ETag = $"\"{payout.Version}\"";
        return Ok(PayoutResponse.FromEntity(payout));
    }
}
