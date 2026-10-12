using System.Globalization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RondiTrack.Api.Authorization;
using RondiTrack.Api.Contracts;
using RondiTrack.Api.Services;
using RondiTrack.Api.Services.Paging;
using RondiTrack.Domain;
using RondiTrack.Domain.Exceptions;
using RondiTrack.Infrastructure.Data;

namespace RondiTrack.Api.Controllers;

[ApiController]
[Route("api/stokvels/{stokvelId:guid}/contributions")]
public class StokvelContributionsController(
    IContributionService contributionService,
    RondiTrackDbContext context) : ApiControllerBase
{
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;

    [HttpGet]
    [EndpointSummary("List a stokvel's contributions, paged")]
    [ProducesResponseType<PageResponse<ContributionResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PageResponse<ContributionResponse>>> GetAll(
        Guid stokvelId,
        [FromQuery] int? pageSize,
        [FromQuery] string? pageToken,
        [FromQuery] string? sort,
        [FromQuery] Guid? cycleId,
        CancellationToken ct)
    {
        if (pageSize is < 0)
            throw new ValidationFailedException("paging.page_size_negative", "Page size cannot be negative.");
        var effectivePageSize = Math.Min(pageSize ?? DefaultPageSize, MaxPageSize);
        if (effectivePageSize == 0) effectivePageSize = DefaultPageSize;

        if (!ContributionSort.TryParse(sort, out var field, out var descending))
            throw new ValidationFailedException("paging.sort_invalid",
                "Sort must be RecordedAt or Amount, optionally followed by _asc or _desc.");

        var sortKey = $"{field}_{(descending ? "desc" : "asc")}";
        var fingerprint = PageToken.Fingerprint(sortKey, cycleId?.ToString());

        var decoded = PageToken.Decode(pageToken);
        if (pageToken is not null && decoded is null)
            throw new ValidationFailedException("paging.token_invalid", "The page token is malformed.");
        if (decoded is not null && decoded.Fingerprint != fingerprint)
            throw new ValidationFailedException("paging.token_mismatch", "This page token is invalid for this query.");

        IQueryable<Contribution> query = context.Contributions.AsNoTracking().Where(c => c.StokvelId == stokvelId);
        if (cycleId is { } cid)
            query = query.Where(c => c.ContributionCycleId == cid);

        if (field == ContributionSortField.RecordedAt)
        {
            if (decoded is not null)
            {
                if (!DateTimeOffset.TryParse(decoded.LastSortValue, CultureInfo.InvariantCulture, DateTimeStyles.None, out var lastValue))
                    throw new ValidationFailedException("paging.token_invalid", "The page token is malformed.");
                var lastId = decoded.LastId;
                query = descending
                    ? query.Where(c => c.RecordedAt < lastValue || (c.RecordedAt == lastValue && c.Id.CompareTo(lastId) < 0))
                    : query.Where(c => c.RecordedAt > lastValue || (c.RecordedAt == lastValue && c.Id.CompareTo(lastId) > 0));
            }
            query = descending
                ? query.OrderByDescending(c => c.RecordedAt).ThenByDescending(c => c.Id)
                : query.OrderBy(c => c.RecordedAt).ThenBy(c => c.Id);
        }
        else
        {
            if (decoded is not null)
            {
                if (!decimal.TryParse(decoded.LastSortValue, NumberStyles.Number, CultureInfo.InvariantCulture, out var lastValue))
                    throw new ValidationFailedException("paging.token_invalid", "The page token is malformed.");
                var lastId = decoded.LastId;
                query = descending
                    ? query.Where(c => c.Amount < lastValue || (c.Amount == lastValue && c.Id.CompareTo(lastId) < 0))
                    : query.Where(c => c.Amount > lastValue || (c.Amount == lastValue && c.Id.CompareTo(lastId) > 0));
            }
            query = descending
                ? query.OrderByDescending(c => c.Amount).ThenByDescending(c => c.Id)
                : query.OrderBy(c => c.Amount).ThenBy(c => c.Id);
        }

        var rows = await query
            .Select(c => new ContributionResponse(c.Id, c.StokvelId, c.UserId, c.ContributionCycleId, c.Amount, c.RecordedAt))
            .Take(effectivePageSize + 1)
            .ToListAsync(ct);

        string? nextToken = null;
        if (rows.Count > effectivePageSize)
        {
            rows.RemoveAt(rows.Count - 1);
            var last = rows[^1];
            var lastSortValue = field == ContributionSortField.RecordedAt
                ? last.RecordedAt.ToString("O", CultureInfo.InvariantCulture)
                : last.Amount.ToString(CultureInfo.InvariantCulture);
            nextToken = PageToken.Encode(new PageTokenData(lastSortValue, last.Id, fingerprint));
        }

        return Ok(new PageResponse<ContributionResponse>(rows, nextToken));
    }

    [HttpGet("{id:guid}")]
    [EndpointSummary("Get one contribution")]
    [ProducesResponseType<ContributionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ContributionResponse>> GetById(Guid stokvelId, Guid id, CancellationToken ct)
    {
        var contribution = await context.Contributions.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id && c.StokvelId == stokvelId, ct)
            ?? throw new NotFoundException("contribution.not_found", $"No contribution with id {id} exists for this stokvel.");
        return Ok(ContributionResponse.FromEntity(contribution));
    }

    [HttpPost]
    [EndpointSummary("Record a contribution (requires an Idempotency-Key header)")]
    [ProducesResponseType<ContributionResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ContributionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ContributionResponse>> Record(
        Guid stokvelId,
        ContributionRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken ct)
    {
        // 6.1: your own payment, or you are a treasurer/chairperson.
        await this.EnsureCanRecordContributionAsync(stokvelId, request.UserId);

        var result = await contributionService.RecordContributionAsync(stokvelId, request, idempotencyKey, ct);

        if (!result.IsSuccess && result.Error.Code == ContributionErrorCodes.IdempotencyKeyReused)
            throw new IdempotencyKeyConflictException(result.Error.Code, result.Error.Message);

        var outcome = result.ThrowIfFailure();

        if (outcome.WasReplayed)
        {
            Response.Headers["Idempotency-Replayed"] = "true";
            return Ok(outcome.Response);
        }

        return CreatedAtAction(nameof(GetById), new { stokvelId, id = outcome.Response.Id }, outcome.Response);
    }
}