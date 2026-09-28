using Microsoft.AspNetCore.Mvc;
using RondiTrack.Api.Contracts;
using RondiTrack.Api.Data;
using RondiTrack.Api.Domain;
using RondiTrack.Api.Exceptions;

namespace RondiTrack.Api.Controllers;

// ---------------------------------------------------------------------------------
// NEW IN 4.3. Plain CRUD, NO SERVICE, on purpose:
//   - Creating a cycle needs exactly ONE existence check (does the stokvel exist?),
//     a single repository lookup with no further decision attached.
//   - That is not the multi-step, cross-entity judgement call that earns a service
//     (compare MembershipService / ContributionService).
// Protected by: validation (shape) + the exception hierarchy (not found) + the entity.
// ---------------------------------------------------------------------------------
[ApiController]
[Route("api/stokvels/{stokvelId:guid}/cycles")]
public class ContributionCyclesController(
    IStokvelRepository stokvels, IContributionCycleRepository cycles) : ApiControllerBase
{
    // GET /api/stokvels/{stokvelId}/cycles
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ContributionCycleResponse>>> GetAll(Guid stokvelId, CancellationToken ct)
    {
        await EnsureStokvelExists(stokvelId, ct);
        var list = await cycles.GetByStokvelAsync(stokvelId, ct);
        return Ok(list.Select(ContributionCycleResponse.FromEntity).ToList());
    }

    // GET /api/stokvels/{stokvelId}/cycles/{id}
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ContributionCycleResponse>> GetById(Guid stokvelId, Guid id, CancellationToken ct)
    {
        var cycle = await GetCycleOrThrow(stokvelId, id, ct);
        return Ok(ContributionCycleResponse.FromEntity(cycle));
    }

    // POST /api/stokvels/{stokvelId}/cycles   -> 201, or 400 / 404
    [HttpPost]
    public async Task<ActionResult<ContributionCycleResponse>> Create(
        Guid stokvelId, ContributionCycleRequest request, CancellationToken ct)
    {
        await EnsureStokvelExists(stokvelId, ct);

        var cycle = ContributionCycle.Create(stokvelId, request.Label, request.TargetAmount).ThrowIfFailure();
        await cycles.AddAsync(cycle, ct);

        var response = ContributionCycleResponse.FromEntity(cycle);
        return CreatedAtAction(nameof(GetById), new { stokvelId, id = response.Id }, response);
    }

    // PUT /api/stokvels/{stokvelId}/cycles/{id}   -> 200, or 400 / 404
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ContributionCycleResponse>> Replace(
        Guid stokvelId, Guid id, ContributionCycleRequest request, CancellationToken ct)
    {
        var cycle = await GetCycleOrThrow(stokvelId, id, ct);
        cycle.Update(request.Label, request.TargetAmount).ThrowIfFailure();
        return Ok(ContributionCycleResponse.FromEntity(cycle));
    }

    // DELETE /api/stokvels/{stokvelId}/cycles/{id}   -> 204, or 404
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid stokvelId, Guid id, CancellationToken ct)
    {
        await GetCycleOrThrow(stokvelId, id, ct);   // 404 if missing or belongs to another stokvel
        await cycles.DeleteAsync(id, ct);
        return NoContent();
    }

    // ---- tiny private helpers: each throws instead of returning an error response ----
    private async Task EnsureStokvelExists(Guid stokvelId, CancellationToken ct)
    {
        _ = await stokvels.GetByIdAsync(stokvelId, ct)
            ?? throw new NotFoundException("stokvel.not_found", $"No stokvel with id {stokvelId} exists.");
    }

    private async Task<ContributionCycle> GetCycleOrThrow(Guid stokvelId, Guid id, CancellationToken ct)
    {
        await EnsureStokvelExists(stokvelId, ct);
        var cycle = await cycles.GetByIdAsync(id, ct);
        if (cycle is null || cycle.StokvelId != stokvelId)
            throw new NotFoundException("cycle.not_found", $"No contribution cycle with id {id} exists for this stokvel.");
        return cycle;
    }
}
