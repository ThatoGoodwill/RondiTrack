using RondiTrack.Api.Domain;
namespace RondiTrack.Api.Data;

public static class SeedData
{
    public static async Task ApplyAsync(IServiceProvider services)
    {
        var users = services.GetRequiredService<IUserRepository>();
        var stokvels = services.GetRequiredService<IStokvelRepository>();

        var thato = Ok(User.Create("Thato", "Matamane", "thato.matamane@example.com", new DateOnly(1988, 3, 14)));
        var sipho = Ok(User.Create("Sipho", "Dlamini", "sipho.dlamini@example.com", new DateOnly(1985, 11, 2)));
        var lerato = Ok(User.Create("Lerato", "Mokoena", "lerato.mokoena@example.com", new DateOnly(1992, 7, 21)));
        var pieter = Ok(User.Create("Pieter", "van der Merwe", "pieter.vdm@example.com", new DateOnly(1979, 1, 30)));
        var aisha = Ok(User.Create("Aisha", "Patel", "aisha.patel@example.com", new DateOnly(1995, 5, 9)));
        var nomsa = Ok(User.Create("Nomsa", "Khumalo", "nomsa.khumalo@example.com", new DateOnly(1990, 9, 17)));
        foreach (var u in new[] { thato, sipho, lerato, pieter, aisha, nomsa }) await users.AddAsync(u);

        var ubuntu = Ok(Stokvel.Create("Ubuntu Savers", 500.00m, ContributionFrequency.Monthly, 10));
        var grocery = Ok(Stokvel.Create("Umoja Grocery Club", 350.50m, ContributionFrequency.Monthly, 12));
        var quick = Ok(Stokvel.Create("Friday Rotation", 100.00m, ContributionFrequency.Weekly, 3));

        Ensure(ubuntu.AddMember(thato.Id)); Ensure(ubuntu.AddMember(sipho.Id)); Ensure(ubuntu.AddMember(lerato.Id));
        Ensure(grocery.AddMember(thato.Id)); Ensure(grocery.AddMember(pieter.Id));
        Ensure(quick.AddMember(sipho.Id)); Ensure(quick.AddMember(aisha.Id)); Ensure(quick.AddMember(pieter.Id)); // now FULL

        foreach (var s in new[] { ubuntu, grocery, quick }) await stokvels.AddAsync(s);
        // Nomsa deliberately joins nothing -> use her to test a clean DELETE
    }

    private static T Ok<T>(Result<T> r) => r.IsSuccess ? r.Value : throw new InvalidOperationException(r.Error.Message);
    private static void Ensure(Result r) { if (!r.IsSuccess) throw new InvalidOperationException(r.Error.Message); }
}