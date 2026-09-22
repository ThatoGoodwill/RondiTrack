using Microsoft.AspNetCore.Mvc;
using RondiTrack.Api.Data;
using RondiTrack.Api.Domain;
namespace RondiTrack.Api.Controllers;

[ApiController]
[Route("api/stokvels")]
public class StokvelsController(IStokvelRepository stokvels) : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<Stokvel>>> GetAll(CancellationToken ct) => Ok(await stokvels.GetAllAsync(ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<Stokvel>> GetById(Guid id, CancellationToken ct)
    {
        var stokvel = await stokvels.GetByIdAsync(id, ct);
        return stokvel is null ? NotFound() : Ok(stokvel);
    }

    [HttpPost]
    public async Task<ActionResult<Stokvel>> Create(StokvelRequest request, CancellationToken ct)
    {
        var result = Stokvel.Create(request.Name, request.ContributionAmount, request.Frequency, request.MaxMembers);
        if (!result.IsSuccess) return ToProblem(result.Error);
        await stokvels.AddAsync(result.Value, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Value.Id }, result.Value);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<Stokvel>> Replace(Guid id, StokvelRequest request, CancellationToken ct)
    {
        var stokvel = await stokvels.GetByIdAsync(id, ct);
        if (stokvel is null) return NotFound();
        var result = stokvel.Update(request.Name, request.ContributionAmount, request.Frequency, request.MaxMembers);
        return !result.IsSuccess ? ToProblem(result.Error) : Ok(stokvel);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) =>
        !await stokvels.DeleteAsync(id, ct) ? NotFound() : NoContent();
}

public sealed record StokvelRequest(string? Name, decimal ContributionAmount, ContributionFrequency Frequency, int MaxMembers);