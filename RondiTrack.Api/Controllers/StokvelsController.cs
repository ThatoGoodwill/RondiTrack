using Microsoft.AspNetCore.Mvc;
using RondiTrack.Api.Contracts;
using RondiTrack.Api.Data;
using RondiTrack.Api.Domain;
using RondiTrack.Api.Exceptions;

namespace RondiTrack.Api.Controllers;

// Plain CRUD talking straight to the repository: NO service on purpose.
// Nothing here needs a decision beyond ONE stokvel's own fields, which the
// Stokvel entity already enforces ("cheap where it's actually cheap").
[ApiController]
[Route("api/stokvels")]
public class StokvelsController(IStokvelRepository stokvels) : ApiControllerBase
{
    // GET /api/stokvels
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<StokvelResponse>>> GetAll(CancellationToken ct)
    {
        var all = await stokvels.GetAllAsync(ct);
        return Ok(all.Select(StokvelResponse.FromEntity).ToList());
    }

    // GET /api/stokvels/{id}   -> 200, or 404 (thrown)
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<StokvelResponse>> GetById(Guid id, CancellationToken ct)
    {
        var stokvel = await stokvels.GetByIdAsync(id, ct)
            ?? throw new NotFoundException("stokvel.not_found", $"No stokvel with id {id} exists.");
        return Ok(StokvelResponse.FromEntity(stokvel));
    }

    // POST /api/stokvels       -> 201, or 400 (thrown by validator / entity)
    [HttpPost]
    public async Task<ActionResult<StokvelResponse>> Create(StokvelRequest request, CancellationToken ct)
    {
        var stokvel = Stokvel.Create(request.Name, request.ContributionAmount, request.Frequency, request.MaxMembers)
            .ThrowIfFailure();

        await stokvels.AddAsync(stokvel, ct);
        var response = StokvelResponse.FromEntity(stokvel);
        return CreatedAtAction(nameof(GetById), new { id = response.Id }, response);
    }

    // PUT /api/stokvels/{id}   -> 200, or 400 / 404 / 409 (max below current member count)
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<StokvelResponse>> Replace(Guid id, StokvelRequest request, CancellationToken ct)
    {
        var stokvel = await stokvels.GetByIdAsync(id, ct)
            ?? throw new NotFoundException("stokvel.not_found", $"No stokvel with id {id} exists.");

        stokvel.Update(request.Name, request.ContributionAmount, request.Frequency, request.MaxMembers)
            .ThrowIfFailure();

        return Ok(StokvelResponse.FromEntity(stokvel));
    }

    // DELETE /api/stokvels/{id} -> 204, or 404 (thrown)
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        if (!await stokvels.DeleteAsync(id, ct))
            throw new NotFoundException("stokvel.not_found", $"No stokvel with id {id} exists.");
        return NoContent();
    }
}
