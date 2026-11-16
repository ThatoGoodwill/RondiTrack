using System.Net;
using System.Net.Http.Json;
using RondiTrack.Api.Contracts;
using RondiTrack.Api.Tests.TestSupport;
using RondiTrack.Domain;

namespace RondiTrack.Api.Tests;

[Collection("Postgres collection")]
public class PayoutOrderTests(PostgresApiFactory factory)
{
    [Fact]
    public async Task Member_can_read_server_order_and_nonmember_gets_404()
    {
        var treasurer = await factory.CreateUserAsync();
        var member = await factory.CreateUserAsync();
        var stranger = await factory.CreateUserAsync();
        var stokvel = await factory.CreateStokvelAsync(
            (treasurer, StokvelMemberRole.Treasurer), (member, StokvelMemberRole.Member));
        var client = factory.CreateClientFor(treasurer);
        var creation = await client.PostAsJsonAsync($"/api/v1/stokvels/{stokvel}/cycles",
            new ContributionCycleRequest($"C-{Guid.NewGuid():N}"[..12], 100m));
        Assert.Equal(HttpStatusCode.Created, creation.StatusCode);
        var cycle = await creation.Content.ReadFromJsonAsync<ContributionCycleResponse>();
        Assert.NotNull(cycle);
        var uri = $"/api/v1/stokvels/{stokvel}/cycles/{cycle!.Id}/payout/order";

        var allowed = await client.GetAsync(uri);
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        var order = await allowed.Content.ReadFromJsonAsync<PayoutOrderResponse>();
        Assert.NotNull(order);
        Assert.Equal(2, order!.Entries.Count);
        Assert.Equal(new[] { 1, 2 }, order.Entries.Select(e => e.Position));
        Assert.Equal("Next", order.Entries[0].Status);
        Assert.Equal("Waiting", order.Entries[1].Status);

        var denied = await factory.CreateClientFor(stranger).GetAsync(uri);
        Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
    }

    [Fact]
    public async Task Wrong_cycle_inside_a_real_stokvel_returns_not_found()
    {
        var member = await factory.CreateUserAsync();
        var stokvel = await factory.CreateStokvelAsync((member, StokvelMemberRole.Member));
        var response = await factory.CreateClientFor(member)
            .GetAsync($"/api/v1/stokvels/{stokvel}/cycles/{Guid.NewGuid()}/payout/order");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
