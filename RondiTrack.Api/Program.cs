using RondiTrack.Api.Data;
using RondiTrack.Api.Services;
using Scalar.AspNetCore;
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
builder.Services.AddOpenApi();
// Repositories (Singleton: one store for the app's whole life)
builder.Services.AddSingleton<IUserRepository, InMemoryUserRepository>();
builder.Services.AddSingleton<IStokvelRepository, InMemoryStokvelRepository>();
builder.Services.AddSingleton<IContributionRepository, InMemoryContributionRepository>();
builder.Services.AddSingleton<IIdempotencyStore, InMemoryIdempotencyStore>();
// Services (also Singleton here: they hold no state of their own, only references to the
// singleton repositories above, so their own lifetime doesn't matter much -- Singleton keeps
// it simple and consistent).
builder.Services.AddSingleton<IUserService, UserService>();
builder.Services.AddSingleton<IMembershipService, MembershipService>();
builder.Services.AddSingleton<IContributionService, ContributionService>();
var app = builder.Build();
await SeedData.ApplyAsync(app.Services);
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}
app.UseHttpsRedirection();
app.MapControllers();
app.Run();