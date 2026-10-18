using System.Net.Http.Headers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RondiTrack.Api.Auth;
using RondiTrack.Domain;
using RondiTrack.Domain.Repositories;
using RondiTrack.Infrastructure.Data;
using Testcontainers.PostgreSql;

namespace RondiTrack.Api.Tests.TestSupport;

public sealed record TestUser(Guid Id, string Email, string Password, bool IsPlatformAdmin);

// ONE throwaway PostgreSQL container for the whole test run (see the collection definition below).
// Because it is a REAL PostgreSQL, the unique index on Users.Email, the composite key on StokvelMembers
// and the xmin concurrency token on RefreshTokens are the ones production uses.
public class PostgresApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    // Test-only values. They exist only inside this test run, like the container itself. The signing key is
    // random per run, so no real-looking secret is committed.
    public const string DefaultPassword = "Correct-Horse-Battery-1";
    private static readonly string TestSigningKey = Convert.ToBase64String(Guid.NewGuid().ToByteArray().Concat(Guid.NewGuid().ToByteArray()).ToArray());

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17")
        .WithDatabase("ronditrack_test")
        .WithUsername("postgres")
        .WithPassword(Guid.NewGuid().ToString("N"))
        .Build();

    
    // 6.2: EVERY log line the app writes during the run is kept here, so a test can prove nothing sensitive was logged.
   
    public CapturingLoggerProvider Logs { get; } = new();
    // Exposed so tests can build their own DbContext against the same container database.
    public string ConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync() => await _container.StartAsync();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // UseSetting, NOT ConfigureAppConfiguration: Program.cs reads these values immediately at startup,
        // and UseSetting is applied before Program.cs runs.
        builder.UseSetting("ConnectionStrings:RondiTrack", _container.GetConnectionString());
        builder.UseSetting("Jwt:SigningKey", TestSigningKey);
                // 6.3: the one allowed frontend origin, and rate limits so generous that tests which are NOT about
        // rate limiting never hit one. The rate-limit tests set their own limits (WithWebHostBuilder + UseSetting).
        builder.UseSetting("Cors:AllowedOrigins:0", "http://localhost:3000");
        builder.UseSetting("RateLimiting:Login:PermitLimit", "100000");
        builder.UseSetting("RateLimiting:Contributions:TokenLimit", "100000");
        builder.UseSetting("RateLimiting:Contributions:TokensPerPeriod", "100000");
        builder.UseSetting("RateLimiting:Payouts:TokenLimit", "100000");
        builder.UseSetting("RateLimiting:Payouts:TokensPerPeriod", "100000");
        builder.UseSetting("CycleClose:Enabled", "false");     // tests construct their own jobs with a fake clock
        builder.UseSetting("Housekeeping:Enabled", "false");
        // SeedUsers:Password is deliberately NOT set: the demo seeder skips, so every test starts from an empty database.

        // 6.2: one extra logger provider that remembers everything the app logs (see CapturingLoggerProvider).
        // OTEL_EXPORTER_OTLP_ENDPOINT is deliberately NOT set either: telemetry is collected and sent nowhere,
        // so these tests never need the dashboard.
        builder.ConfigureLogging(logging => logging.AddProvider(Logs));
    }

    async Task IAsyncLifetime.DisposeAsync() => await _container.DisposeAsync();

    // ---------- clients ----------

    // https, so the Secure refresh cookie behaves as in production. HandleCookies=false: tests read and send
    // the cookie by hand, because replaying an OLD cookie is exactly what some tests check.
    public HttpClient CreateAnonymousClient() => CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost"),
        HandleCookies = false
    });

    // A client already signed in as this user. The token is minted by the app's OWN TokenService, so it is
    // signed with the same key and has the same claims a real login would produce.
    public HttpClient CreateClientFor(TestUser user)
    {
        var client = CreateAnonymousClient();
        var tokenService = Services.GetRequiredService<TokenService>();
        var role = user.IsPlatformAdmin ? PlatformRoles.PlatformAdmin : PlatformRoles.User;
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", tokenService.CreateAccessToken(user.Id, role).Token);
        return client;
    }

    public string AccessTokenFor(TestUser user)
    {
        var role = user.IsPlatformAdmin ? PlatformRoles.PlatformAdmin : PlatformRoles.User;
        return Services.GetRequiredService<TokenService>().CreateAccessToken(user.Id, role).Token;
    }

    // Every pre-6.1 test was written for "can do anything". A platform admin keeps it that way, so no
    // existing assertion has to change: those tests simply swap CreateClient() for CreateAdminClient().
    public async Task<HttpClient> CreateAdminClientAsync() => CreateClientFor(await CreateUserAsync(platformAdmin: true));

    // Same thing for constructors, which cannot await.
    public HttpClient CreateAdminClient() => CreateAdminClientAsync().GetAwaiter().GetResult();

    // ---------- seeding helpers (straight into the database, through the app's own repositories) ----------

    public async Task<TestUser> CreateUserAsync(bool platformAdmin = false, string? email = null)
    {
        email ??= $"user-{Guid.NewGuid():N}@tests.ronditrack.test";
        using var scope = Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>();

        var user = User.Create("Test", "User", email, new DateOnly(1990, 1, 1)).Value;
        user.SetPasswordHash(hasher.HashPassword(user, DefaultPassword));
        if (platformAdmin) user.GrantPlatformAdmin();
        await users.AddAsync(user);

        return new TestUser(user.Id, user.Email, DefaultPassword, platformAdmin);
    }

    // A stokvel with the given members, each with the given role. Returns the stokvel id.
    public async Task<Guid> CreateStokvelAsync(params (TestUser User, StokvelMemberRole Role)[] members)
    {
        using var scope = Services.CreateScope();
        var stokvels = scope.ServiceProvider.GetRequiredService<IStokvelRepository>();

        var stokvel = Stokvel.Create($"Test Stokvel {Guid.NewGuid():N}"[..30], 100m, ContributionFrequency.Monthly, 20).Value;
        foreach (var (user, role) in members)
        {
            stokvel.AddMember(user.Id).ThrowIfFailure();
            stokvel.GetMember(user.Id)!.ChangeRole(role);
        }
        await stokvels.AddAsync(stokvel);
        return stokvel.Id;
    }

    public async Task<Guid> CreateCycleAsync(Guid stokvelId)
    {
        using var scope = Services.CreateScope();
        var cycles = scope.ServiceProvider.GetRequiredService<IContributionCycleRepository>();
        var cycle = ContributionCycle.Create(stokvelId, $"C-{Guid.NewGuid():N}"[..12], 100m).Value;
        await cycles.AddAsync(cycle);
        return cycle.Id;
    }

    // Changes a member's role directly in the database, the way a chairperson's action would.
    public async Task ChangeRoleAsync(Guid stokvelId, Guid userId, StokvelMemberRole newRole)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RondiTrackDbContext>();
        var member = await db.StokvelMembers.SingleAsync(m => m.StokvelId == stokvelId && m.UserId == userId);
        member.ChangeRole(newRole);
        await db.SaveChangesAsync();
    }

    public async Task<bool> AnyRefreshTokenHashEqualsAsync(string value)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RondiTrackDbContext>();
        return await db.RefreshTokens.AnyAsync(t => t.TokenHash == value);
    }
}

// xUnit collection fixture: every test class in this collection shares ONE container and ONE database.
[CollectionDefinition("Postgres collection")]
public class PostgresCollection : ICollectionFixture<PostgresApiFactory> { }
