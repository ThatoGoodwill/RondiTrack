using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using RondiTrack.Api.Data;
using RondiTrack.Api.Exceptions;
using RondiTrack.Api.OpenApi;
using RondiTrack.Api.Services;
using RondiTrack.Api.Validators;
using Scalar.AspNetCore;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ======================= 1. REGISTER SERVICES (dependency injection) =======================

// Controllers + the global ValidationFilter (runs before EVERY action, validates every DTO).
builder.Services.AddControllers(options => options.Filters.Add<ValidationFilter>());

// Stop ASP.NET's built-in automatic 400 for unreadable bodies. Our ValidationFilter turns those
// into the SAME problem+json shape (with correlationId) as every other error.
builder.Services.Configure<ApiBehaviorOptions>(options => options.SuppressModelStateInvalidFilter = true);

// Finds and registers EVERY AbstractValidator<T> in this project automatically.
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

// OpenAPI, with our custom example transformer (4.4) attaching realistic sample bodies.
builder.Services.AddOpenApi(options =>
{
    options.AddOperationTransformer<ExampleOperationTransformer>();
});

// Repositories + idempotency store. Singleton = ONE store for the whole life of the app,
// otherwise data would vanish between requests.
builder.Services.AddSingleton<IUserRepository, InMemoryUserRepository>();
builder.Services.AddSingleton<IStokvelRepository, InMemoryStokvelRepository>();
builder.Services.AddSingleton<IContributionRepository, InMemoryContributionRepository>();
builder.Services.AddSingleton<IContributionCycleRepository, InMemoryContributionCycleRepository>();
builder.Services.AddSingleton<IIdempotencyStore, InMemoryIdempotencyStore>();

// Services (the decision-makers). ContributionCycle deliberately has NO service.
builder.Services.AddSingleton<IUserService, UserService>();
builder.Services.AddSingleton<IMembershipService, MembershipService>();
builder.Services.AddSingleton<IContributionService, ContributionService>();



builder.Services.AddDbContext<RondiTrack.Api.Data.RondiTrackDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("RondiTrack")));

builder.Services.AddScoped<IStokvelMemberRepository, SqlStokvelMemberRepository>();
// ---- Centralized error handling ----
builder.Services.AddExceptionHandler<RondiTrackExceptionHandler>();

// ProblemDetails support for everything ELSE (e.g. unknown routes like /api/users/banana),
// with the same correlationId field so even those bare 404s share the standard shape.
builder.Services.AddProblemDetails(options =>
    options.CustomizeProblemDetails = context =>
        context.ProblemDetails.Extensions["correlationId"] = context.HttpContext.TraceIdentifier);

var app = builder.Build();

// Seed the demo data once at startup.
await SeedData.ApplyAsync(app.Services);

// ======================= 2. BUILD THE REQUEST PIPELINE (order matters) =======================

app.UseExceptionHandler();   // FIRST: catches anything thrown further down and hands it to our handler
app.UseStatusCodePages();    // turns empty 4xx responses (unknown route, 405...) into problem+json

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();
app.MapControllers();

app.Run();

// Makes the auto-generated Program class visible to the test project (WebApplicationFactory<Program>).
public partial class Program
{
}
