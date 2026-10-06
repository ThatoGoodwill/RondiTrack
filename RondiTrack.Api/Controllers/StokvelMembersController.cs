using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RondiTrack.Api.Contracts;
using RondiTrack.Api.Data;
using RondiTrack.Api.Domain;
using RondiTrack.Api.Exceptions;
using RondiTrack.Api.Services;
using RondiTrack.Api.Services.Paging;

namespace RondiTrack.Api.Controllers;

[ApiController]
[Route("api/stokvels/{stokvelId:guid}/members")]
public class StokvelMembersController(
    RondiTrackDbContext context,
    IStokvelRepository stokvels,
    IMembershipService membershipService) : ApiControllerBase
{
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;

    [HttpGet]
    [EndpointSummary("List a stokvel's members, paged")]
    [ProducesResponseType<PageResponse<MembershipResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PageResponse<MembershipResponse>>> GetAll(
        Guid stokvelId,
        [FromQuery] int? pageSize,
        [FromQuery] string? pageToken,
        CancellationToken ct)
    {
        if (pageSize is < 0)
            throw new ValidationFailedException("paging.page_size_negative", "Page size cannot be negative.");
        var effectivePageSize = Math.Min(pageSize ?? DefaultPageSize, MaxPageSize);
        if (effectivePageSize == 0) effectivePageSize = DefaultPageSize;

        const string fingerprint = "JoinedAtUtc_asc|";

        var decoded = PageToken.Decode(pageToken);
        if (pageToken is not null && decoded is null)
            throw new ValidationFailedException("paging.token_invalid", "The page token is malformed.");
        if (decoded is not null && decoded.Fingerprint != fingerprint)
            throw new ValidationFailedException("paging.token_mismatch", "This page token is invalid for this query.");

        _ = await context.Stokvels.AsNoTracking().FirstOrDefaultAsync(s => s.Id == stokvelId, ct)
            ?? throw new NotFoundException("stokvel.not_found", $"No stokvel with id {stokvelId} exists.");

        IQueryable<StokvelMember> query = context.StokvelMembers.AsNoTracking()
            .Where(m => m.StokvelId == stokvelId)
            .OrderBy(m => m.JoinedAtUtc).ThenBy(m => m.UserId);

        if (decoded is not null)
        {
            var lastJoined = DateTimeOffset.Parse(decoded.LastSortValue);
            query = query.Where(m => m.JoinedAtUtc > lastJoined ||
                                     (m.JoinedAtUtc == lastJoined && m.UserId.CompareTo(decoded.LastId) > 0));
        }

        var rows = await query
            .Select(m => new MembershipResponse(m.StokvelId, m.UserId, m.Role.ToString(), m.JoinedAtUtc))
            .Take(effectivePageSize + 1)
            .ToListAsync(ct);

        string? nextToken = null;
        if (rows.Count > effectivePageSize)
        {
            rows.RemoveAt(rows.Count - 1);
            var last = rows[^1];
            nextToken = PageToken.Encode(new PageTokenData(last.JoinedAtUtc.ToString("O"), last.UserId, fingerprint));
        }

        return Ok(new PageResponse<MembershipResponse>(rows, nextToken));
    }

    [HttpGet("{userId:guid}")]
    [EndpointSummary("Get one membership")]
    [ProducesResponseType<MembershipResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MembershipResponse>> GetById(Guid stokvelId, Guid userId, CancellationToken ct)
    {
        var stokvel = await stokvels.GetByIdAsync(stokvelId, ct)
            ?? throw new NotFoundException("stokvel.not_found", $"No stokvel with id {stokvelId} exists.");
        var membership = stokvel.GetMember(userId)
            ?? throw new NotFoundException("membership.not_found", "This user is not a member of this stokvel.");
        return Ok(MembershipResponse.FromEntity(membership));
    }

    [HttpPost]
    [EndpointSummary("Add a member to a stokvel")]
    [ProducesResponseType<MembershipResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<MembershipResponse>> Add(Guid stokvelId, AddMemberRequest request, CancellationToken ct)
    {
        var membership = (await membershipService.AddMemberAsync(stokvelId, request.UserId, ct)).ThrowIfFailure();
        var response = MembershipResponse.FromEntity(membership);
        return CreatedAtAction(nameof(GetById), new { stokvelId, userId = response.UserId }, response);
    }

    [HttpDelete("{userId:guid}")]
    [EndpointSummary("Remove a member from a stokvel")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Remove(Guid stokvelId, Guid userId, CancellationToken ct)
    {
        (await membershipService.RemoveMemberAsync(stokvelId, userId, ct)).ThrowIfFailure();
        return NoContent();
    }
}