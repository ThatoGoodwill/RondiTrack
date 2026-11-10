using System.Globalization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RondiTrack.Api.Auth;
using RondiTrack.Api.Contracts;
using RondiTrack.Api.Services.Paging;
using RondiTrack.Domain.Exceptions;
using RondiTrack.Infrastructure.Data;

namespace RondiTrack.Api.Controllers;

public sealed record ActivityResponse(Guid Id, Guid StokvelId, string Kind, DateTimeOffset OccurredAt,
    long? AmountCents, string Description);
internal sealed record ActivitySourceRow(Guid Id, Guid StokvelId, DateTimeOffset OccurredAt,
    decimal? Amount, string Kind);

[ApiController]
[Route(ApiRoutes.Base + "/activity")]
public sealed class ActivityController(RondiTrackDbContext db) : ApiControllerBase
{
    [HttpGet]
    [EndpointSummary("Newest activity from the caller's stokvels, paged")]
    [ProducesResponseType<PageResponse<ActivityResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PageResponse<ActivityResponse>>> GetAll(
        [FromQuery] int? pageSize, [FromQuery] string? pageToken, CancellationToken ct)
    {
        var userId = User.GetUserId();
        if (userId is null) return Unauthorized();
        if (pageSize is < 1 or > 100)
            throw new ValidationFailedException("paging.page_size_invalid", "Page size must be from 1 to 100.");
        var size = pageSize ?? 10;
        var fingerprint = PageToken.Fingerprint("activity_desc", userId.Value.ToString());
        var cursor = PageToken.Decode(pageToken);
        if (pageToken is not null && cursor is null)
            throw new ValidationFailedException("paging.token_invalid", "The page token is malformed.");
        if (cursor is not null && cursor.Fingerprint != fingerprint)
            throw new ValidationFailedException("paging.token_mismatch", "This page token is invalid for this query.");
        DateTimeOffset? after = null;
        if (cursor is not null)
        {
            if (!DateTimeOffset.TryParse(cursor.LastSortValue, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var value))
                throw new ValidationFailedException("paging.token_invalid", "The page token is malformed.");
            after = value;
        }
        var accessible = db.StokvelMembers.AsNoTracking()
            .Where(m => m.UserId == userId.Value).Select(m => m.StokvelId);
        var contributions = db.Contributions.AsNoTracking().Where(c => accessible.Contains(c.StokvelId));
        var payouts = db.Payouts.AsNoTracking().Where(p => accessible.Contains(p.StokvelId));
        var memberships = db.StokvelMembers.AsNoTracking().Where(m => accessible.Contains(m.StokvelId));
        if (after is { } last)
        {
            var id = cursor!.LastId;
            contributions = contributions.Where(c => c.RecordedAt < last || (c.RecordedAt == last && c.Id.CompareTo(id) < 0));
            payouts = payouts.Where(p => p.ProcessedAt < last || (p.ProcessedAt == last && p.Id.CompareTo(id) < 0));
            memberships = memberships.Where(m => m.JoinedAtUtc < last || (m.JoinedAtUtc == last && m.UserId.CompareTo(id) < 0));
        }
        var paid = await contributions.OrderByDescending(c => c.RecordedAt).ThenByDescending(c => c.Id)
            .Select(c => new ActivitySourceRow(c.Id, c.StokvelId, c.RecordedAt, c.Amount, "contribution"))
            .Take(size + 1).ToListAsync(ct);
        var disbursed = await payouts.OrderByDescending(p => p.ProcessedAt).ThenByDescending(p => p.Id)
            .Select(p => new ActivitySourceRow(p.Id, p.StokvelId, p.ProcessedAt, p.Amount, "payout"))
            .Take(size + 1).ToListAsync(ct);
        var joined = await memberships.OrderByDescending(m => m.JoinedAtUtc).ThenByDescending(m => m.UserId)
            .Select(m => new ActivitySourceRow(m.UserId, m.StokvelId, m.JoinedAtUtc, null, "member_joined"))
            .Take(size + 1).ToListAsync(ct);
        var combined = paid.Concat(disbursed).Concat(joined)
            .OrderByDescending(row => row.OccurredAt).ThenByDescending(row => row.Id)
            .Take(size + 1).ToList();
        string? next = null;
        if (combined.Count > size)
        {
            combined.RemoveAt(size);
            var lastRow = combined[^1];
            next = PageToken.Encode(new PageTokenData(lastRow.OccurredAt.ToString("O", CultureInfo.InvariantCulture),
                lastRow.Id, fingerprint));
        }
        var items = combined.Select(r => new ActivityResponse(r.Id, r.StokvelId, r.Kind, r.OccurredAt,
            r.Amount.HasValue ? ContributionResponse.ToCents(r.Amount.Value) : null,
            r.Kind switch { "contribution" => "Contribution recorded", "payout" => "Payout made", _ => "Member joined" }))
            .ToList();
        return Ok(new PageResponse<ActivityResponse>(items, next));
    }
}
