using Microsoft.EntityFrameworkCore;
using RondiTrack.Infrastructure.Data;
using RondiTrack.Domain;

// Run manually with: dotnet run --project RondiTrack.SeedVolume -- "<your connection string>"
// NEVER wired into RondiTrack.Api's own Program.cs -- this only runs when YOU explicitly invoke it.

if (args.Length == 0)
{
    Console.WriteLine("Usage: dotnet run -- \"<connection string>\"");
    return;
}

var options = new DbContextOptionsBuilder<RondiTrackDbContext>()
    .UseNpgsql(args[0])
    .Options;

await using var context = new RondiTrackDbContext(options);

var stokvel = await context.Stokvels.FirstOrDefaultAsync(s => s.Name == "Volume Test Stokvel");
if (stokvel is null)
{
    stokvel = Stokvel.Create("Volume Test Stokvel", 100m, ContributionFrequency.Monthly, 50).Value;
    context.Stokvels.Add(stokvel);
    await context.SaveChangesAsync();
}

var cycle = await context.ContributionCycles.FirstOrDefaultAsync(c => c.StokvelId == stokvel.Id && c.Label == "Volume-2026");
if (cycle is null)
{
    cycle = ContributionCycle.Create(stokvel.Id, "Volume-2026", 100m).Value;
    context.ContributionCycles.Add(cycle);
    await context.SaveChangesAsync();
}

// Need real member users to attach contributions to -- create a pool of 50 and add them all.
var users = new List<User>();
for (var i = 0; i < 50; i++)
{
    var user = User.Create($"Seed{i}", "User", $"seedvolume{i}@example.com", new DateOnly(1990, 1, 1)).Value;
    users.Add(user);
    context.Users.Add(user);
}
await context.SaveChangesAsync();

foreach (var user in users)
{
    if (!await context.StokvelMembers.AnyAsync(m => m.StokvelId == stokvel.Id && m.UserId == user.Id))
        context.StokvelMembers.Add(StokvelMember.Create(stokvel.Id, user.Id));
}
await context.SaveChangesAsync();

const int target = 12_000;   // comfortably above the brief's 10,000 floor
var random = new Random(42);
var batch = new List<Contribution>();

for (var i = 0; i < target; i++)
{
    var user = users[random.Next(users.Count)];
    var contribution = Contribution.Create(stokvel.Id, user.Id, cycle.Id, 100m);
    if (contribution.IsSuccess) batch.Add(contribution.Value);

    if (batch.Count == 1000)
    {
        context.Contributions.AddRange(batch);
        await context.SaveChangesAsync();
        batch.Clear();
        Console.WriteLine($"Inserted {i + 1} / {target}");
    }
}
if (batch.Count > 0)
{
    context.Contributions.AddRange(batch);
    await context.SaveChangesAsync();
}

Console.WriteLine("Done.");