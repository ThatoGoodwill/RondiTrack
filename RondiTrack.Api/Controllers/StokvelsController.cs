using Microsoft.AspNetCore.Mvc;
using RondiTrack.Api.Contracts;
using RondiTrack.Api.Data;
using RondiTrack.Api.Domain;
namespace RondiTrack.Api.Controllers;
[ApiController]
[Route("api/stokvels")]
public class StokvelsController(IStokvelRepository stokvels) : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<StokvelResponse>>> GetAll(CancellationToken ct)
    {
        var all = await stokvels.GetAllAsync(ct);
        return Ok(all.Select(StokvelResponse.FromEntity).ToList());
    }
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<StokvelResponse>> GetById(Guid id, CancellationToken ct)
    {
        var stokvel = await stokvels.GetByIdAsync(id, ct);
        return stokvel is null
            ? NotFoundProblem("stokvel.not_found", $"No stokvel with id {id} exists.")
            : Ok(StokvelResponse.FromEntity(stokvel));
    }
    [HttpPost]
    public async Task<ActionResult<StokvelResponse>> Create(StokvelRequest request, CancellationToken ct)
    {
        var result = Stokvel.Create(request.Name, request.ContributionAmount, request.Frequency, request.MaxMembers);
        if (!result.IsSuccess) return ToProblem(result.Error);
        await stokvels.AddAsync(result.Value, ct);
        var response = StokvelResponse.FromEntity(result.Value);
        return CreatedAtAction(nameof(GetById), new { id = response.Id }, response);
    }
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<StokvelResponse>> Replace(Guid id, StokvelRequest request, CancellationToken ct)
    {
        var stokvel = await stokvels.GetByIdAsync(id, ct);
        if (stokvel is null) return NotFoundProblem("stokvel.not_found", $"No stokvel with id {id} exists.");
        var result = stokvel.Update(request.Name, request.ContributionAmount, request.Frequency, request.MaxMembers);
        return !result.IsSuccess ? ToProblem(result.Error) : Ok(StokvelResponse.FromEntity(stokvel));
    }
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) =>
        !await stokvels.DeleteAsync(id, ct)
            ? NotFoundProblem("stokvel.not_found", $"No stokvel with id {id} exists.")
            : NoContent();
}