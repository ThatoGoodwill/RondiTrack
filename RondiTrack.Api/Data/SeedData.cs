using RondiTrack.Api.Domain;

namespace RondiTrack.Api.Data;

// ---------------------------------------------------------------------------------
// Starter demo data, created ONCE at startup (called from Program.cs).
// It uses the SAME Create() factories a real request would use, so even the seed data
// must obey every rule. If a value below breaks a rule the app fails fast at startup.
//
// CHANGED IN 4.3: also seeds one ContributionCycle ("2026-09") per stokvel, because a
// Contribution must now reference a real cycle.
// ---------------------------------------------------------------------------------
public static class SeedData
{
    public static async Task ApplyAsync(IServiceProvider services)
    {
        var users = services.GetRequiredService<IUserRepository>();
        var stokvels = services.GetRequiredService<IStokvelRepository>();
        var cycles = services.GetRequiredService<IContributionCycleRepository>();

        // ---- users ----
        var thandi = Ok(User.Create("Thandi", "Nkosi", "thandi.nkosi@example.com", new DateOnly(1988, 3, 14)));
        var sipho  = Ok(User.Create("Sipho", "Dlamini", "sipho.dlamini@example.com", new DateOnly(1985, 11, 2)));
        var lerato = Ok(User.Create("Lerato", "Mokoena", "lerato.mokoena@example.com", new DateOnly(1992, 7, 21)));
        var pieter = Ok(User.Create("Pieter", "van der Merwe", "pieter.vdm@example.com", new DateOnly(1979, 1, 30)));
        var aisha  = Ok(User.Create("Aisha", "Patel", "aisha.patel@example.com", new DateOnly(1995, 5, 9)));
        var nomsa  = Ok(User.Create("Nomsa", "Khumalo", "nomsa.khumalo@example.com", new DateOnly(1990, 9, 17)));

        foreach (var u in new[] { thandi, sipho, lerato, pieter, aisha, nomsa })
            await users.AddAsync(u);

        // ---- stokvels ----
        var ubuntu  = Ok(Stokvel.Create("Ubuntu Savers", 500.00m, ContributionFrequency.Monthly, 10));
        var grocery = Ok(Stokvel.Create("Umoja Grocery Club", 350.50m, ContributionFrequency.Monthly, 12));
        var friday  = Ok(Stokvel.Create("Friday Rotation", 100.00m, ContributionFrequency.Weekly, 3));   // small: easy to fill

        // ---- memberships (the Stokvel entity enforces its own rules even here) ----
        Ensure(ubuntu.AddMember(thandi.Id));
        Ensure(ubuntu.AddMember(sipho.Id));
        Ensure(ubuntu.AddMember(lerato.Id));
        Ensure(grocery.AddMember(thandi.Id));
        Ensure(grocery.AddMember(pieter.Id));
        Ensure(friday.AddMember(sipho.Id));
        Ensure(friday.AddMember(aisha.Id));
        Ensure(friday.AddMember(pieter.Id));          // Friday Rotation is now FULL (3 of 3)

        foreach (var s in new[] { ubuntu, grocery, friday })
            await stokvels.AddAsync(s);

        // ---- one September 2026 cycle per stokvel ----
        await cycles.AddAsync(Ok(ContributionCycle.Create(ubuntu.Id, "2026-09", 500.00m)));
        await cycles.AddAsync(Ok(ContributionCycle.Create(grocery.Id, "2026-09", 350.50m)));
        await cycles.AddAsync(Ok(ContributionCycle.Create(friday.Id, "2026-W39", 100.00m)));

        // Nomsa deliberately joins nothing: use her to test a clean DELETE /api/users/{id}.
    }

    // Unwrap a Result<T> or crash loudly: seed data must always be valid.
    private static T Ok<T>(Result<T> result) =>
        result.IsSuccess ? result.Value
            : throw new InvalidOperationException($"Invalid seed data: {result.Error.Message}");

    private static void Ensure(Result result)
    {
        if (!result.IsSuccess)
            throw new InvalidOperationException($"Invalid seed data: {result.Error.Message}");
    }
}
