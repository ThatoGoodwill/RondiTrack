using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RondiTrack.Domain;
using RondiTrack.Domain.Repositories;
using RondiTrack.Infrastructure.Data;

namespace RondiTrack.Api.Seeding;

// Demo data for LOCAL development. Replaces the old in-memory SeedData.
//  - Idempotent: does nothing if any user already exists, so restarting never duplicates rows.
//  - The password for every demo account comes from user-secrets (SeedUsers:Password). It is never in source.
//  - Shows the point of 6.1: Lerato is a plain Member of "Ubuntu Savers" but Treasurer of "Umoja Grocery Club".
public static class DevelopmentSeeder
{
    public static async Task ApplyAsync(IServiceProvider services, string? seedPassword, ILogger logger)
    {
        if (string.IsNullOrWhiteSpace(seedPassword))
        {
            logger.LogInformation("SeedUsers:Password is not set; skipping demo data.");
            return;
        }

        var db = services.GetRequiredService<RondiTrackDbContext>();
        if (await db.Users.AnyAsync()) return;

        var users = services.GetRequiredService<IUserRepository>();
        var stokvels = services.GetRequiredService<IStokvelRepository>();
        var cycles = services.GetRequiredService<IContributionCycleRepository>();
        var contributions = services.GetRequiredService<IContributionRepository>();
        var hasher = services.GetRequiredService<IPasswordHasher<User>>();
        

        User NewUser(string first, string last, string email, DateOnly dob)
        {
            var user = Ok(User.Create(first, last, email, dob));
            user.SetPasswordHash(hasher.HashPassword(user, seedPassword));
            return user;
        }

        var admin  = NewUser("Platform", "Admin", "admin@ronditrack.test", new DateOnly(1985, 1, 1));
        admin.GrantPlatformAdmin();
        var thandi = NewUser("Thandi", "Nkosi", "thandi.nkosi@example.com", new DateOnly(1988, 3, 14));
        var sipho  = NewUser("Sipho", "Dlamini", "sipho.dlamini@example.com", new DateOnly(1985, 11, 2));
        var lerato = NewUser("Lerato", "Mokoena", "lerato.mokoena@example.com", new DateOnly(1992, 7, 21));
        var pieter = NewUser("Pieter", "van der Merwe", "pieter.vdm@example.com", new DateOnly(1979, 1, 30));
        var aisha  = NewUser("Aisha", "Patel", "aisha.patel@example.com", new DateOnly(1995, 5, 9));
        var nomsa  = NewUser("Nomsa", "Khumalo", "nomsa.khumalo@example.com", new DateOnly(1990, 9, 17));

        foreach (var u in new[] { admin, thandi, sipho, lerato, pieter, aisha, nomsa })
            await users.AddAsync(u);

        var ubuntu  = Ok(Stokvel.Create("Ubuntu Savers", 500.00m, ContributionFrequency.Monthly, 10));
        var grocery = Ok(Stokvel.Create("Umoja Grocery Club", 350.50m, ContributionFrequency.Monthly, 12));
        var friday  = Ok(Stokvel.Create("Friday Rotation", 100.00m, ContributionFrequency.Weekly, 3));

        Join(ubuntu,  thandi, StokvelMemberRole.Admin);
        Join(ubuntu,  sipho,  StokvelMemberRole.Treasurer);
        Join(ubuntu,  lerato, StokvelMemberRole.Member);      // Member here...
        Join(grocery, pieter, StokvelMemberRole.Admin);
        Join(grocery, lerato, StokvelMemberRole.Treasurer);   // ...Treasurer here, same login
        Join(grocery, thandi, StokvelMemberRole.Member);
        Join(friday,  sipho,  StokvelMemberRole.Admin);
        Join(friday,  aisha,  StokvelMemberRole.Member);
        Join(friday,  pieter, StokvelMemberRole.Member);

        foreach (var s in new[] { ubuntu, grocery, friday })
            await stokvels.AddAsync(s);

                var ubuntuCycle  = Ok(ContributionCycle.Create(ubuntu.Id,  "2026-09", 500.00m));
        var groceryCycle = Ok(ContributionCycle.Create(grocery.Id, "2026-09", 350.50m));
        var fridayCycle  = Ok(ContributionCycle.Create(friday.Id,  "2026-W39", 100.00m));

        // Friday Rotation's only cycle is PaidOut, so Lerato's Friday card will show "No open cycle".
        fridayCycle.MarkPaidOut();

        await cycles.AddAsync(ubuntuCycle);
        await cycles.AddAsync(groceryCycle);
        await cycles.AddAsync(fridayCycle);

        // Captured sample data for Assignment 7.1, one contribution per (stokvel, user, cycle)
        // so the duplicate-payment guard is satisfied.
        //   Ubuntu (Lerato = Member):    10 cents paid, still owes R499.90 → "Still owed" on the card
        //   Grocery (Lerato = Treasurer): full R350.50 paid → "All paid up for this cycle"
        //   Two other members' contributions exercise ContributionCard on real rows.
        await contributions.AddAsync(Ok(Contribution.Create(ubuntu.Id,  lerato.Id, ubuntuCycle.Id,  0.10m)));
        await contributions.AddAsync(Ok(Contribution.Create(grocery.Id, lerato.Id, groceryCycle.Id, 350.50m)));
        await contributions.AddAsync(Ok(Contribution.Create(ubuntu.Id,  thandi.Id, ubuntuCycle.Id,  500.00m)));
        await contributions.AddAsync(Ok(Contribution.Create(grocery.Id, pieter.Id, groceryCycle.Id, 100.00m)));

        logger.LogInformation("Demo data created (7 users, 3 stokvels, 4 contributions).");
       
    }

    private static void Join(Stokvel stokvel, User user, StokvelMemberRole role)
    {
        var added = stokvel.AddMember(user.Id);
        if (!added.IsSuccess) throw new InvalidOperationException($"Invalid seed data: {added.Error.Message}");
        stokvel.GetMember(user.Id)!.ChangeRole(role);
    }

    private static T Ok<T>(Result<T> result) =>
        result.IsSuccess ? result.Value
            : throw new InvalidOperationException($"Invalid seed data: {result.Error.Message}");
}
