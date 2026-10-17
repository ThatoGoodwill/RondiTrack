using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RondiTrack.Api.Services;
using RondiTrack.Domain;
using RondiTrack.Domain.Repositories;
using RondiTrack.Infrastructure.Data;

namespace RondiTrack.Api.Tests;

[Collection("Postgres collection")]
public sealed class InfrastructureTests(PostgresApiFactory factory)
{
    [Fact]
    public async Task App_uses_the_container_database_and_all_migrations_are_applied()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RondiTrackDbContext>();

       Assert.Equal(factory.ConnectionString, db.Database.GetConnectionString());

var applied = (await db.Database.GetAppliedMigrationsAsync()).ToList();
// Every migration that exists in code must have been applied to this container.
var expected = db.Database.GetMigrations().OrderBy(m => m).ToList();
Assert.Equal(expected, applied.OrderBy(m => m).ToList());
    }

    [Fact]
    public void Every_registered_dependency_resolves_without_lifetime_errors()
    {
        // The test host runs in Development, where ASP.NET Core rejects a Singleton that holds a Scoped service.
        using var scope = factory.Services.CreateScope();
        var sp = scope.ServiceProvider;

        Assert.NotNull(sp.GetRequiredService<IUserRepository>());
        Assert.NotNull(sp.GetRequiredService<IStokvelRepository>());
        Assert.NotNull(sp.GetRequiredService<IContributionRepository>());
        Assert.NotNull(sp.GetRequiredService<IContributionCycleRepository>());
        Assert.NotNull(sp.GetRequiredService<IIdempotencyStore>());
        Assert.NotNull(sp.GetRequiredService<IStokvelMemberRepository>());
        Assert.NotNull(sp.GetRequiredService<IUserService>());
        Assert.NotNull(sp.GetRequiredService<IMembershipService>());
        Assert.NotNull(sp.GetRequiredService<IContributionService>());
        Assert.NotNull(sp.GetRequiredService<RondiTrackDbContext>());
    }

    [Fact]
    public async Task Unknown_payout_returns_404_problem_json_from_the_database()
    {
        var client = factory.CreateAdminClient();

        var response = await client.GetAsync($"/api/v1/payouts/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("payout.not_found", body.RootElement.GetProperty("title").GetString());
    }

    [Fact]
    public async Task Registering_a_user_returns_201_and_the_user_can_be_read_back()
    {
        var client = factory.CreateAdminClient();
        var request = new
        {
            firstName = "Thandi",
            lastName = "Test",
            email = $"thandi.{Guid.NewGuid():N}@example.com",
            dateOfBirth = "1990-01-01"
        };

        var created = await client.PostAsJsonAsync("/api/v1/users", request);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var location = created.Headers.Location;
        Assert.NotNull(location);
        var read = await client.GetAsync(location);
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
    }

    [Fact]
    public async Task Duplicate_membership_is_rejected_by_the_composite_key_in_real_PostgreSQL()
    {
        var stokvel = Stokvel.Create("Composite Key Stokvel", 100m, ContributionFrequency.Monthly, 5).Value;
        var user = User.Create("Ada", "Test", $"ada.{Guid.NewGuid():N}@example.com", new DateOnly(1990, 1, 1)).Value;

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RondiTrackDbContext>();
            db.Stokvels.Add(stokvel);
            db.Users.Add(user);
            db.StokvelMembers.Add(StokvelMember.Create(stokvel.Id, user.Id));
            await db.SaveChangesAsync();
        }

        // A second scope = a second DbContext that knows nothing about the first row, so the DATABASE must refuse it.
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RondiTrackDbContext>();
            db.StokvelMembers.Add(StokvelMember.Create(stokvel.Id, user.Id));
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        }
    }
}