using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Filters;
using RondiTrack.Domain.Exceptions;

namespace RondiTrack.Api.Authorization;

// Registered ONCE, globally (Program.cs). It runs before every action; for any route containing
// {stokvelId} it asks the handler "is this caller allowed in THIS stokvel?" BEFORE the controller runs.
// New controllers under /api/stokvels/{stokvelId}/... are protected without anyone remembering anything.
public sealed class StokvelScopeFilter(StokvelAccessChecker checker) : IAsyncAuthorizationFilter
{
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        if (!context.RouteData.Values.TryGetValue("stokvelId", out var raw)) return;

        var endpoint = context.HttpContext.GetEndpoint();
        if (endpoint?.Metadata.GetMetadata<IAllowAnonymous>() is not null) return;

        if (!Guid.TryParse(Convert.ToString(raw, CultureInfo.InvariantCulture), out var stokvelId))
            throw new NotFoundException("stokvel.not_found", "No such stokvel exists.");

        var policy = endpoint?.Metadata.GetMetadata<StokvelAccessAttribute>()?.Policy ?? Policies.StokvelMember;

        await checker.EnsureAsync(context.HttpContext.User, stokvelId, policy);
    }
}
