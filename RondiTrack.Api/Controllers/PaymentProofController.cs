using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RondiTrack.Api.Auth;
using RondiTrack.Api.Authorization;
using RondiTrack.Domain;
using RondiTrack.Domain.Exceptions;
using RondiTrack.Infrastructure.Data;

namespace RondiTrack.Api.Controllers;

public sealed record PaymentProofResponse(Guid ContributionId, string MediaType, DateTimeOffset UploadedAtUtc);

[ApiController]
[Route(ApiRoutes.Base + "/stokvels/{stokvelId:guid}/contributions/{contributionId:guid}/proof")]
public sealed class PaymentProofController(RondiTrackDbContext db) : ApiControllerBase
{
    public const long MaxFileBytes = 5 * 1024 * 1024;

    [HttpPut]
    [RequestSizeLimit(6 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 6 * 1024 * 1024)]
    [Consumes("multipart/form-data")]
    [ProducesResponseType<PaymentProofResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PaymentProofResponse>> Upload(
        Guid stokvelId, Guid contributionId, IFormFile? file, CancellationToken ct)
    {
        var userId = User.GetUserId() ?? throw new AuthenticationFailedException("auth.required", "Sign in first.");
        var contribution = await db.Contributions.AsNoTracking()
            .SingleOrDefaultAsync(c => c.StokvelId == stokvelId && c.Id == contributionId, ct)
            ?? throw Missing();
        if (contribution.UserId != userId)
            throw Missing(); // Do not reveal whether another member uploaded a bank document.
        var cycle = await db.ContributionCycles.AsNoTracking()
            .SingleOrDefaultAsync(c => c.Id == contribution.ContributionCycleId, ct)
            ?? throw Missing();
        if (cycle.Status != ContributionCycleStatus.Open)
            throw new ConflictException("proof.cycle_closed", "Proofs cannot be changed after the cycle closes.");
        if (file is null || file.Length == 0 || file.Length > MaxFileBytes)
            throw new ValidationFailedException("proof.invalid_size", "Choose a document up to 5 MB.");
        await using var stream = file.OpenReadStream(MaxFileBytes + 1);
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, ct);
        if (buffer.Length > MaxFileBytes)
            throw new ValidationFailedException("proof.invalid_size", "Choose a document up to 5 MB.");
        var data = buffer.ToArray();
        var mime = DetectMime(data) ?? throw new ValidationFailedException("proof.invalid_type",
            "Only real PDF, JPEG or PNG files are allowed.");
        var proof = await db.Set<PaymentProof>().FirstOrDefaultAsync(p => p.ContributionId == contributionId, ct);
        if (proof is null)
        {
            proof = PaymentProof.Create(stokvelId, contributionId, userId, mime, data);
            db.Set<PaymentProof>().Add(proof);
        }
        else proof.Replace(mime, data);
        await db.SaveChangesAsync(ct);
        return Ok(new PaymentProofResponse(contributionId, mime, proof.UploadedAtUtc));
    }

    [HttpGet]
    public async Task<IActionResult> Download(Guid stokvelId, Guid contributionId, CancellationToken ct)
    {
        var userId = User.GetUserId() ?? throw Missing();
        var contribution = await db.Contributions.AsNoTracking()
            .SingleOrDefaultAsync(c => c.StokvelId == stokvelId && c.Id == contributionId, ct) ?? throw Missing();
        // Per-stokvel role comes from the DB, never from the user's JWT.
        var member = await db.StokvelMembers.AsNoTracking()
            .SingleOrDefaultAsync(m => m.StokvelId == stokvelId && m.UserId == userId, ct);
        if (contribution.UserId != userId && (member is null || member.Role < StokvelMemberRole.Treasurer))
            throw Missing();
        var proof = await db.Set<PaymentProof>().AsNoTracking()
            .SingleOrDefaultAsync(p => p.StokvelId == stokvelId && p.ContributionId == contributionId, ct)
            ?? throw Missing();
        Response.Headers["Cache-Control"] = "private, no-store";
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        var ext = proof.MimeType switch { "application/pdf" => ".pdf", "image/png" => ".png", _ => ".jpg" };
        return File(proof.Bytes, proof.MimeType, "proof-of-payment" + ext);
    }

    // Never trust a .pdf extension or browser-controlled Content-Type; check magic bytes.
    internal static string? DetectMime(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length >= 5 && bytes[..5].SequenceEqual("%PDF-"u8)) return "application/pdf";
        if (bytes.Length >= 8 && bytes[..8].SequenceEqual(new byte[] { 137,80,78,71,13,10,26,10 })) return "image/png";
        if (bytes.Length >= 3 && bytes[..3].SequenceEqual(new byte[] { 255,216,255 })) return "image/jpeg";
        return null;
    }
    private static NotFoundException Missing() => new("proof.not_found", "Proof of payment was not found.");
}
