using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RondiTrack.Api.Contracts;
using RondiTrack.Api.Controllers;
using RondiTrack.Api.Tests.TestSupport;
using RondiTrack.Domain;
using RondiTrack.Infrastructure.Data;

namespace RondiTrack.Api.Tests;

[Collection("Postgres collection")]
public sealed class WeekNineFlowTests(PostgresApiFactory factory)
{
    private static HttpRequestMessage WithKey(string path, object payload, string key)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(payload) };
        request.Headers.TryAddWithoutValidation("Idempotency-Key", key);
        return request;
    }

    [Fact]
    public async Task Joining_twice_never_adds_two_memberships()
    {
        var treasurer = await factory.CreateUserAsync();
        var invitee = await factory.CreateUserAsync();
        var stokvel = await factory.CreateStokvelAsync((treasurer, StokvelMemberRole.Treasurer));
        var created = await factory.CreateClientFor(treasurer).PostAsync($"/api/v1/stokvels/{stokvel}/invites", null);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var code = (await created.Content.ReadFromJsonAsync<InviteCreatedResponse>())!.Code;
        var client = factory.CreateClientFor(invitee);
        var preview = await client.GetAsync($"/api/v1/invites/{code}");
        Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
        var json = await preview.Content.ReadAsStringAsync();
        Assert.DoesNotContain("payoutOrder", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("balances", json, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync($"/api/v1/invites/{code}/join", null)).StatusCode);
        var again = await client.PostAsync($"/api/v1/invites/{code}/join", null);
        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
        Assert.True((await again.Content.ReadFromJsonAsync<InviteJoinResponse>())!.AlreadyMember);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RondiTrackDbContext>();
        Assert.Equal(1, await db.StokvelMembers.CountAsync(m => m.StokvelId == stokvel && m.UserId == invitee.Id));
    }

    [Fact]
    public async Task Close_refuses_stale_preview_and_replays_same_key()
    {
        var treasurer = await factory.CreateUserAsync();
        var member = await factory.CreateUserAsync();
        var stokvel = await factory.CreateStokvelAsync((treasurer, StokvelMemberRole.Treasurer),
            (member, StokvelMemberRole.Member));
        var cycle = await factory.CreateCycleAsync(stokvel);
        var treasurerClient = factory.CreateClientFor(treasurer);
        var path = $"/api/v1/stokvels/{stokvel}/cycles/{cycle}";
        var initial = (await (await treasurerClient.GetAsync(path + "/close-preview"))
            .Content.ReadFromJsonAsync<CycleClosePreview>())!;
        var contribution = await factory.CreateClientFor(member).SendAsync(WithKey(
            $"/api/v1/stokvels/{stokvel}/cycles/{cycle}/contributions",
            new { userId = member.Id, amountInCents = 15050L }, Guid.NewGuid().ToString()));
        Assert.Equal(HttpStatusCode.Created, contribution.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await treasurerClient.SendAsync(WithKey(path + "/close",
            new ConfirmCloseRequest(initial.Version), Guid.NewGuid().ToString()))).StatusCode);
        var fresh = (await (await treasurerClient.GetAsync(path + "/close-preview"))
            .Content.ReadFromJsonAsync<CycleClosePreview>())!;
        Assert.NotEqual(initial.Version, fresh.Version);
        var key = Guid.NewGuid().ToString();
        Assert.Equal(HttpStatusCode.OK, (await treasurerClient.SendAsync(WithKey(path + "/close",
            new ConfirmCloseRequest(fresh.Version), key))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await treasurerClient.SendAsync(WithKey(path + "/close",
            new ConfirmCloseRequest(fresh.Version), key))).StatusCode);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RondiTrackDbContext>();
        Assert.Equal(fresh.Members.Count, await db.ArrearsRecords.CountAsync(a => a.ContributionCycleId == cycle));
    }
}
