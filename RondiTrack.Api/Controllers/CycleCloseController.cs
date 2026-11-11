using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RondiTrack.Api.Authorization;
using RondiTrack.Api.Services;
using RondiTrack.Domain;
using RondiTrack.Domain.Exceptions;
using RondiTrack.Infrastructure.Data;

namespace RondiTrack.Api.Controllers;

public sealed record CycleCloseMember(Guid UserId, string DisplayName, long ArrearsCents);
public sealed record CycleClosePreview(
    Guid CycleId, string Label, DateOnly AsOfSouthAfricanDate,
    string Version, long TotalArrearsCents, IReadOnlyList<CycleCloseMember> Members);
public sealed record ConfirmCloseRequest(string? PreviewVersion);
public sealed record CycleCloseReceipt(Guid CycleId, string Status, long TotalArrearsCents, int ArrearsCount);

[ApiController]
[Route(ApiRoutes.Base + "/stokvels/{stokvelId:guid}/cycles/{cycleId:guid}")]
[StokvelAccess(Policies.StokvelTreasurer)]
public sealed class CycleCloseController(RondiTrackDbContext db) : ApiControllerBase
{
    [HttpGet("close-preview")]
    public async Task<ActionResult<CycleClosePreview>> Preview(Guid stokvelId, Guid cycleId, CancellationToken ct) =>
        Ok(await CalculatePreview(stokvelId, cycleId, ct));

    [HttpPost("close")]
    public async Task<ActionResult<CycleCloseReceipt>> Close(Guid stokvelId, Guid cycleId,
        ConfirmCloseRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey) || idempotencyKey.Length > 200)
            throw new ValidationFailedException("cycle.idempotency_required", "An Idempotency-Key header is required.");
        if (string.IsNullOrWhiteSpace(request.PreviewVersion))
            throw new ValidationFailedException("cycle.preview_required", "Review the close preview first.");
        var hash = IdempotencyHasher.Compute(new { stokvelId, cycleId, request.PreviewVersion });
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var previous = await db.IdempotencyKeys.AsNoTracking().SingleOrDefaultAsync(k => k.Key == idempotencyKey, ct);
        if (previous is not null)
        {
            if (previous.RequestHash != hash)
                throw new ConflictException("idempotency.key_reused", "This key belongs to a different close request.");
            var existingReceipt = JsonSerializer.Deserialize<CycleCloseReceipt>(previous.ResponseBodyJson)!;
            return Ok(existingReceipt); // Same key: no second arrears record or close.
        }

        var preview = await CalculatePreview(stokvelId, cycleId, ct);
        if (!string.Equals(request.PreviewVersion, preview.Version, StringComparison.Ordinal))
            throw new ConflictException("cycle.preview_stale", "The cycle changed. Reload the preview and confirm again.");
        var affected = await db.ContributionCycles
            .Where(c => c.StokvelId == stokvelId && c.Id == cycleId && c.Status == ContributionCycleStatus.Open)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(c => c.Status, ContributionCycleStatus.Closed)
                .SetProperty(c => c.ClosedAt, (DateTimeOffset?)DateTimeOffset.UtcNow), ct);
        if (affected != 1)
            throw new ConflictException("cycle.already_closed", "The cycle was already closed. Reload its status.");
        foreach (var item in preview.Members)
            db.ArrearsRecords.Add(ArrearsRecord.Create(stokvelId, cycleId,
                item.UserId, item.ArrearsCents, DateTimeOffset.UtcNow));
        var receipt = new CycleCloseReceipt(cycleId, "Closed", preview.TotalArrearsCents, preview.Members.Count);
        db.IdempotencyKeys.Add(new IdempotencyKeyEntry(idempotencyKey, hash,
            JsonSerializer.Serialize(receipt), DateTimeOffset.UtcNow));
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return Ok(receipt);
    }

    private async Task<CycleClosePreview> CalculatePreview(Guid stokvelId, Guid cycleId, CancellationToken ct)
    {
        var cycle = await db.ContributionCycles.AsNoTracking()
            .SingleOrDefaultAsync(c => c.StokvelId == stokvelId && c.Id == cycleId, ct)
            ?? throw new NotFoundException("cycle.not_found", "Cycle not found.");
        if (cycle.Status != ContributionCycleStatus.Open)
            throw new ConflictException("cycle.not_open", "This cycle is not open for closing.");
        var all = await (from m in db.StokvelMembers.AsNoTracking()
            join u in db.Users.AsNoTracking() on m.UserId equals u.Id
            where m.StokvelId == stokvelId
            orderby m.UserId
            select new { m.UserId, Name = u.FirstName + " " + u.LastName }).ToListAsync(ct);
        var paid = await db.Contributions.AsNoTracking()
            .Where(c => c.ContributionCycleId == cycleId && c.StokvelId == stokvelId)
            .GroupBy(c => c.UserId)
            .Select(g => new { UserId = g.Key, Paid = g.Sum(c => c.Amount) })
            .ToDictionaryAsync(p => p.UserId, p => p.Paid, ct);
        long target = (long)(cycle.TargetAmount * 100m);
        var arrears = all.Select(m => new CycleCloseMember(m.UserId, m.Name,
            CycleArrearsRules.ShortfallCents(target,
                (long)(paid.GetValueOrDefault(m.UserId) * 100m))))
            .Where(x => x.ArrearsCents > 0).ToList();
        var snapshot = string.Join("|", all.Select(m =>
            m.UserId.ToString("D") + ":" + paid.GetValueOrDefault(m.UserId).ToString(CultureInfo.InvariantCulture)));
        var source = $"{cycleId:D}|{cycle.Label}|{target}|{cycle.Status}|{snapshot}";
        var version = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(source))).ToLowerInvariant();
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(
            DateTimeOffset.UtcNow, "Africa/Johannesburg").DateTime);
        return new CycleClosePreview(cycleId, cycle.Label, today, version,
            arrears.Sum(x => x.ArrearsCents), arrears);
    }
}
