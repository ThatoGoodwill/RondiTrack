using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using RondiTrack.Api.Auth;
using RondiTrack.Api.Authorization;
using RondiTrack.Api.Contracts;
using RondiTrack.Api.Frontend;
using RondiTrack.Api.Services;
using RondiTrack.Domain;
using RondiTrack.Domain.Exceptions;
using RondiTrack.Infrastructure.Data;

namespace RondiTrack.Api.Controllers;

[ApiController]
[Route(ApiRoutes.Base + "/stokvels/{stokvelId:guid}/invites")]
[StokvelAccess(Policies.StokvelTreasurer)]
public sealed class StokvelInvitesController(RondiTrackDbContext db) : ApiControllerBase
{
    [HttpPost]
    [ProducesResponseType<InviteCreatedResponse>(StatusCodes.Status201Created)]
    public async Task<ActionResult<InviteCreatedResponse>> Create(Guid stokvelId, CancellationToken ct)
    {
        var me = User.GetUserId() ?? throw new AuthenticationFailedException("auth.required", "Sign in first.");
        // 192-bit random code. The DB only receives the digest, not the link-bearing secret.
        var code = Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();
        var expires = DateTimeOffset.UtcNow.AddDays(7);
        db.Set<InviteCode>().Add(InviteCode.Create(Hash(code), stokvelId, me, expires));
        await db.SaveChangesAsync(ct);
        return StatusCode(StatusCodes.Status201Created,
            new InviteCreatedResponse(code, "/join?code=" + Uri.EscapeDataString(code), expires));
    }

    internal static string Hash(string code) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(code))).ToLowerInvariant();
}

[ApiController]
[Authorize]
[Route(ApiRoutes.Base + "/invites")]
public sealed class InvitesController(RondiTrackDbContext db, IMembershipService members) : ApiControllerBase
{
    [HttpGet("{code}")]
    [EnableRateLimiting(RateLimitPolicies.Login)]
    [ProducesResponseType<InvitePreviewResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<InvitePreviewResponse>> Preview(string code, CancellationToken ct)
    {
        var invite = await FindLiveInvite(code, ct);
        var stokvel = await db.Stokvels.AsNoTracking().Where(s => s.Id == invite.StokvelId)
            .Select(s => new { s.Id, s.Name, s.ContributionAmount, s.Frequency, s.MaxMembers,
                MemberCount = s.Members.Count }).SingleOrDefaultAsync(ct)
            ?? throw InvalidInvite();
        return Ok(new InvitePreviewResponse(stokvel.Id, stokvel.Name,
            ContributionResponse.ToCents(stokvel.ContributionAmount), stokvel.Frequency.ToString(),
            stokvel.MemberCount, stokvel.MaxMembers,
            "Members contribute the agreed amount each cycle. Payouts follow the stokvel's agreed rotation.",
            invite.ExpiresAtUtc));
    }

    [HttpPost("{code}/join")]
    [EnableRateLimiting(RateLimitPolicies.Login)]
    [ProducesResponseType<InviteJoinResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<InviteJoinResponse>> Join(string code, CancellationToken ct)
    {
        var me = User.GetUserId() ?? throw new AuthenticationFailedException("auth.required", "Sign in first.");
        var invite = await FindLiveInvite(code, ct);
        // Repeated join is a successful no-op. The composite membership PK guards races.
        if (await db.StokvelMembers.AsNoTracking().AnyAsync(
            m => m.StokvelId == invite.StokvelId && m.UserId == me, ct))
            return Ok(new InviteJoinResponse(invite.StokvelId, true));
        (await members.AddMemberAsync(invite.StokvelId, me, ct)).ThrowIfFailure();
        return Ok(new InviteJoinResponse(invite.StokvelId, false));
    }

    private async Task<InviteCode> FindLiveInvite(string code, CancellationToken ct)
    {
        if (code.Length != 48 || !code.All(Uri.IsHexDigit)) throw InvalidInvite();
        var hash = StokvelInvitesController.Hash(code.ToLowerInvariant());
        return await db.Set<InviteCode>().AsNoTracking()
            .Where(i => i.CodeHash == hash && i.ExpiresAtUtc > DateTimeOffset.UtcNow)
            .FirstOrDefaultAsync(ct) ?? throw InvalidInvite();
    }
    private static NotFoundException InvalidInvite() =>
        new("invite.not_found", "This invite is invalid or has expired. Ask for a new invite.");
}
