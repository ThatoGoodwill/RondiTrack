using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using RondiTrack.Api.Contracts;
using RondiTrack.Api.Tests.TestSupport;
using RondiTrack.Domain;

namespace RondiTrack.Api.Tests;

// 6.1: authorization = "what may you do IN THIS STOKVEL?". The role comes from the database on every
// request, so these tests change the database and reuse the SAME token to prove it.
[Collection("Postgres collection")]
public class StokvelAuthorizationTests(PostgresApiFactory factory)
{
    private static ContributionCycleRequest NewCycle() => new($"C-{Guid.NewGuid():N}"[..12], 100m);

    private static async Task<JsonElement> ReadProblemAsync(HttpResponseMessage response)
    {
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static Task<HttpResponseMessage> RecordContributionAsync(
        HttpClient client, Guid stokvelId, Guid cycleId, Guid forUserId)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/stokvels/{stokvelId}/contributions")
        {
            Content = JsonContent.Create(new ContributionRequest(forUserId, cycleId, 100m))
        };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        return client.SendAsync(request);
    }

    // ---------- ONE endpoint, three callers: POST /api/v1/stokvels/{id}/cycles ----------

    [Fact]
    public async Task Non_member_is_404_on_open_cycle()
    {
        var treasurer = await factory.CreateUserAsync();
        var stranger = await factory.CreateUserAsync();
        var stokvelId = await factory.CreateStokvelAsync((treasurer, StokvelMemberRole.Treasurer));

        var response = await factory.CreateClientFor(stranger)
            .PostAsJsonAsync($"/api/v1/stokvels/{stokvelId}/cycles", NewCycle());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        await ReadProblemAsync(response);
    }

    [Fact]
    public async Task Member_without_the_role_is_403_on_open_cycle()
    {
        var member = await factory.CreateUserAsync();
        var stokvelId = await factory.CreateStokvelAsync((member, StokvelMemberRole.Member));

        var response = await factory.CreateClientFor(member)
            .PostAsJsonAsync($"/api/v1/stokvels/{stokvelId}/cycles", NewCycle());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await ReadProblemAsync(response);
    }

    [Theory]
    [InlineData(StokvelMemberRole.Treasurer)]
    [InlineData(StokvelMemberRole.Admin)]   // Admin = chairperson
    public async Task Treasurer_or_chairperson_can_open_a_cycle(StokvelMemberRole role)
    {
        var user = await factory.CreateUserAsync();
        var stokvelId = await factory.CreateStokvelAsync((user, role));

        var response = await factory.CreateClientFor(user)
            .PostAsJsonAsync($"/api/v1/stokvels/{stokvelId}/cycles", NewCycle());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    // ---------- 404 must not leak ----------

    [Fact]
    public async Task A_stokvel_you_are_not_in_looks_exactly_like_one_that_does_not_exist()
    {
        var owner = await factory.CreateUserAsync();
        var stranger = await factory.CreateUserAsync();
        var real = await factory.CreateStokvelAsync((owner, StokvelMemberRole.Admin));
        var client = factory.CreateClientFor(stranger);

        var existing = await client.GetAsync($"/api/v1/stokvels/{real}");
        var missing = await client.GetAsync($"/api/v1/stokvels/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, existing.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        var a = await ReadProblemAsync(existing);
        var b = await ReadProblemAsync(missing);
        Assert.Equal(a.GetProperty("type").GetString(), b.GetProperty("type").GetString());
        Assert.Equal(a.GetProperty("title").GetString(), b.GetProperty("title").GetString());
    }

    // ---------- members can view ----------

    [Fact]
    public async Task A_plain_member_can_view_the_stokvel_its_cycles_and_its_contributions()
    {
        var member = await factory.CreateUserAsync();
        var stokvelId = await factory.CreateStokvelAsync((member, StokvelMemberRole.Member));
        var client = factory.CreateClientFor(member);

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/v1/stokvels/{stokvelId}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/v1/stokvels/{stokvelId}/cycles")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/v1/stokvels/{stokvelId}/members")).StatusCode);
    }

    // ---------- recording a contribution ----------

    [Fact]
    public async Task A_member_can_record_their_own_contribution_but_not_someone_elses()
    {
        var member = await factory.CreateUserAsync();
        var other = await factory.CreateUserAsync();
        var stokvelId = await factory.CreateStokvelAsync(
            (member, StokvelMemberRole.Member), (other, StokvelMemberRole.Member));
        var cycleId = await factory.CreateCycleAsync(stokvelId);
        var client = factory.CreateClientFor(member);

        var mine = await RecordContributionAsync(client, stokvelId, cycleId, member.Id);
        var theirs = await RecordContributionAsync(client, stokvelId, cycleId, other.Id);

        Assert.True(mine.IsSuccessStatusCode, $"own contribution failed: {(int)mine.StatusCode}");
        Assert.Equal(HttpStatusCode.Forbidden, theirs.StatusCode);
    }

    [Fact]
    public async Task A_treasurer_can_record_a_contribution_for_any_member()
    {
        var treasurer = await factory.CreateUserAsync();
        var member = await factory.CreateUserAsync();
        var stokvelId = await factory.CreateStokvelAsync(
            (treasurer, StokvelMemberRole.Treasurer), (member, StokvelMemberRole.Member));
        var cycleId = await factory.CreateCycleAsync(stokvelId);

        var response = await RecordContributionAsync(factory.CreateClientFor(treasurer), stokvelId, cycleId, member.Id);

        Assert.True(response.IsSuccessStatusCode, $"treasurer recording failed: {(int)response.StatusCode}");
    }

    // ---------- payouts ----------

    [Fact]
    public async Task Processing_a_payout_needs_treasurer_or_chairperson()
    {
        var member = await factory.CreateUserAsync();
        var stranger = await factory.CreateUserAsync();
        var stokvelId = await factory.CreateStokvelAsync((member, StokvelMemberRole.Member));
        var cycleId = await factory.CreateCycleAsync(stokvelId);
        var url = $"/api/v1/stokvels/{stokvelId}/cycles/{cycleId}/payout";

        Assert.Equal(HttpStatusCode.Forbidden, (await factory.CreateClientFor(member).PostAsync(url, null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await factory.CreateClientFor(stranger).PostAsync(url, null)).StatusCode);
    }

    // ---------- the point of checking the database: the token can be stale, the role cannot ----------

    [Fact]
    public async Task A_demoted_treasurer_loses_access_immediately_with_the_same_token()
    {
        var treasurer = await factory.CreateUserAsync();
        var stokvelId = await factory.CreateStokvelAsync((treasurer, StokvelMemberRole.Treasurer));
        var client = factory.CreateClientFor(treasurer);   // ONE token, minted once

        var before = await client.PostAsJsonAsync($"/api/v1/stokvels/{stokvelId}/cycles", NewCycle());
        Assert.Equal(HttpStatusCode.Created, before.StatusCode);

        await factory.ChangeRoleAsync(stokvelId, treasurer.Id, StokvelMemberRole.Member);   // "10:00: removed as treasurer"

        var after = await client.PostAsJsonAsync($"/api/v1/stokvels/{stokvelId}/cycles", NewCycle());   // "10:04: same token"
        Assert.Equal(HttpStatusCode.Forbidden, after.StatusCode);
    }

    [Fact]
    public async Task One_login_can_hold_different_roles_in_different_stokvels()
    {
        var lerato = await factory.CreateUserAsync();
        var boss = await factory.CreateUserAsync();
        var burialSociety = await factory.CreateStokvelAsync((lerato, StokvelMemberRole.Treasurer));
        var workClub = await factory.CreateStokvelAsync((boss, StokvelMemberRole.Admin), (lerato, StokvelMemberRole.Member));
        var client = factory.CreateClientFor(lerato);   // same token for both calls

        Assert.Equal(HttpStatusCode.Created,
            (await client.PostAsJsonAsync($"/api/v1/stokvels/{burialSociety}/cycles", NewCycle())).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await client.PostAsJsonAsync($"/api/v1/stokvels/{workClub}/cycles", NewCycle())).StatusCode);
    }

    // ---------- platform level ----------

    [Fact]
    public async Task Listing_every_stokvel_is_for_platform_admins_only()
    {
        var user = await factory.CreateUserAsync();
        var admin = await factory.CreateUserAsync(platformAdmin: true);

        Assert.Equal(HttpStatusCode.Forbidden, (await factory.CreateClientFor(user).GetAsync("/api/v1/stokvels")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await factory.CreateClientFor(admin).GetAsync("/api/v1/stokvels")).StatusCode);
    }

    [Fact]
    public async Task A_platform_admin_can_act_in_any_stokvel()
    {
        var owner = await factory.CreateUserAsync();
        var admin = await factory.CreateUserAsync(platformAdmin: true);
        var stokvelId = await factory.CreateStokvelAsync((owner, StokvelMemberRole.Member));

        var response = await factory.CreateClientFor(admin)
            .PostAsJsonAsync($"/api/v1/stokvels/{stokvelId}/cycles", NewCycle());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task An_ordinary_user_cannot_list_or_delete_other_users()
    {
        var user = await factory.CreateUserAsync();
        var client = factory.CreateClientFor(user);

        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/users")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.DeleteAsync($"/api/v1/users/{Guid.NewGuid()}")).StatusCode);
    }

    // ---------- fail closed: a forgotten route cannot be unscoped ----------

    [Fact]
    public void Every_route_under_api_stokvels_carries_a_stokvelId_so_the_scope_filter_applies()
    {
        var endpoints = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>();

        var offenders = endpoints
            .Select(e => e.RoutePattern.RawText ?? string.Empty)
            .Where(route => route.TrimStart('/').StartsWith("api/stokvels/", StringComparison.OrdinalIgnoreCase))
            .Where(route => !route.Contains("{stokvelId", StringComparison.Ordinal))
            .ToList();

        Assert.Empty(offenders);
    }
}
