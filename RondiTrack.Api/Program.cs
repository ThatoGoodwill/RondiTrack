using System.Text;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Npgsql;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using RondiTrack.Api.Auth;
using RondiTrack.Api.Authorization;
using RondiTrack.Api.Exceptions;
using RondiTrack.Api.OpenApi;
using RondiTrack.Api.Observability;
using RondiTrack.Api.Seeding;
using RondiTrack.Api.Services;
using RondiTrack.Api.Validators;
using RondiTrack.Domain;
using RondiTrack.Domain.Repositories;
using RondiTrack.Infrastructure.Data;
using Scalar.AspNetCore;
using RondiTrack.Api.Frontend;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// ======================= 1. REGISTER SERVICES (dependency injection) =======================

// Controllers + two GLOBAL filters:
//   ValidationFilter   - validates every request DTO (4.x)
//   StokvelScopeFilter - 6.1: any route with {stokvelId} must pass the stokvel-scoped authorization first
builder.Services.AddControllers(options =>
{
    options.Filters.Add<StokvelScopeFilter>();
    options.Filters.Add<ValidationFilter>();
})
.AddJsonOptions(o => o.JsonSerializerOptions.NumberHandling = JsonNumberHandling.Strict);

// The OpenAPI generator reads THESE options. Strict here is what removes "or string" from the generated document.
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.NumberHandling = JsonNumberHandling.Strict);

builder.Services.Configure<ApiBehaviorOptions>(options => options.SuppressModelStateInvalidFilter = true);
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

// OpenAPI + a Bearer scheme so Scalar shows a box for pasting the access token.
builder.Services.AddOpenApi(options =>
{
    options.AddOperationTransformer<ExampleOperationTransformer>();
    options.AddDocumentTransformer((document, context, cancellationToken) =>
    {
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = "Paste the accessToken returned by POST /api/auth/login."
        };
        document.Security =
        [
            new OpenApiSecurityRequirement { [new OpenApiSecuritySchemeReference("Bearer", document)] = [] }
        ];
        return Task.CompletedTask;
    });
});

// ---- Secrets: fail at startup, with the fix in the message ----
var connectionString = builder.Configuration.GetConnectionString("RondiTrack")
    ?? throw new InvalidOperationException(
        "Setting 'ConnectionStrings__RondiTrack' is missing (the database connection string). " +
        "In Docker or on a server set it as the environment variable ConnectionStrings__RondiTrack. " +
        "For local development: dotnet user-secrets set \"ConnectionStrings:RondiTrack\" \"<your connection string>\" --project RondiTrack.Api");

var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
jwtOptions.EnsureValid();
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));

// ---- Authentication: "who are you?" ----
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;   // keep "sub" and "role" as they are in the token

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtOptions.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],   // never let the token pick its own algorithm
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),                // default is 5 minutes, which would stretch our short tokens
            NameClaimType = JwtRegisteredClaimNames.Sub,
            RoleClaimType = AppClaims.Role
        };

        // 401 and 403 from the auth middleware use the SAME problem+json shape as every other error.
        options.Events = new JwtBearerEvents
        {
            OnChallenge = async context =>
            {
                context.HandleResponse();
                var expired = context.AuthenticateFailure is SecurityTokenExpiredException;
                context.Response.Headers.WWWAuthenticate = expired
                    ? "Bearer error=\"invalid_token\", error_description=\"The token has expired\""
                    : "Bearer";
                await ProblemResponseWriter.WriteAsync(context.HttpContext, StatusCodes.Status401Unauthorized,
                    expired ? "auth.token_expired" : "auth.unauthenticated",
                    expired ? "Your access token has expired. Refresh it and try again."
                            : "Authentication is required to access this resource.");
            },
            OnForbidden = context =>
            {
                // 6.2: a 403 from the platform-role check is an authorization refusal too: count and log it.
                AuthorizationRefusals.ReportForbidden(context.HttpContext);
                return ProblemResponseWriter.WriteAsync(context.HttpContext,
                    StatusCodes.Status403Forbidden, "auth.forbidden", "You do not have permission to perform this action.");
            }
        };
    });

// ---- Authorization: "what may you do?" ----
builder.Services.AddAuthorization(options =>
{
    // Secure by default: an endpoint with no [Authorize] / [AllowAnonymous] still needs a signed-in user.
    options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();

    // Policy on the token's PLATFORM role (a policy, so many endpoints can share one rule).
    options.AddPolicy(Policies.PlatformAdmin, p => p.RequireRole(PlatformRoles.PlatformAdmin));

    // Policies on the caller's role IN ONE STOKVEL (looked up in the database by the handlers).
    options.AddPolicy(Policies.StokvelMember,
        p => p.AddRequirements(new StokvelRoleRequirement(StokvelMemberRole.Member)));
    options.AddPolicy(Policies.StokvelTreasurer,
        p => p.AddRequirements(new StokvelRoleRequirement(StokvelMemberRole.Treasurer)));
    options.AddPolicy(Policies.StokvelChairperson,
        p => p.AddRequirements(new StokvelRoleRequirement(StokvelMemberRole.Admin)));
    options.AddPolicy(Policies.RecordContribution,
        p => p.AddRequirements(new RecordContributionRequirement()));
});

// Handlers are Scoped: they read the database through a Scoped repository.
builder.Services.AddScoped<IAuthorizationHandler, StokvelRoleHandler>();
builder.Services.AddScoped<IAuthorizationHandler, RecordContributionHandler>();
builder.Services.AddScoped<StokvelAccessChecker>();

builder.Services.AddSingleton(TimeProvider.System);

// 6.2: the business counters (one Meter for the whole API). Singleton: counters are created once and shared.
builder.Services.AddSingleton<RondiTrackMetrics>();
builder.Services.AddSingleton<TokenService>();
builder.Services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();

// ---- Data: ONE store (PostgreSQL). Every repository that holds a DbContext is Scoped. ----
builder.Services.AddDbContext<RondiTrackDbContext>(options => options.UseNpgsql(connectionString));

builder.Services.AddScoped<IUserRepository, EfUserRepository>();
builder.Services.AddScoped<IStokvelRepository, EfStokvelRepository>();
builder.Services.AddScoped<IStokvelMemberRepository, SqlStokvelMemberRepository>();
builder.Services.AddScoped<IContributionRepository, EfContributionRepository>();
builder.Services.AddScoped<IContributionCycleRepository, EfContributionCycleRepository>();
builder.Services.AddScoped<IRefreshTokenRepository, EfRefreshTokenRepository>();

// 6.4: idempotency keys live in PostgreSQL (Scoped, because it holds a DbContext).
builder.Services.AddScoped<IIdempotencyStore, EfIdempotencyStore>();

// Services depend on Scoped repositories, so they are Scoped too.
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IMembershipService, MembershipService>();
// 6.2: ContributionService does the work; the Instrumented wrapper counts each NEW contribution.
builder.Services.AddScoped<ContributionService>();
builder.Services.AddScoped<IContributionService>(sp => new InstrumentedContributionService(
    sp.GetRequiredService<ContributionService>(), sp.GetRequiredService<RondiTrackMetrics>()));
// 6.2: PayoutService does the work; the Instrumented wrapper owns the payout.process span and the payout counter.
builder.Services.AddScoped<PayoutService>();
builder.Services.AddScoped<IPayoutService>(sp => new InstrumentedPayoutService(
    sp.GetRequiredService<PayoutService>(), sp.GetRequiredService<RondiTrackMetrics>()));
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<IHomeService, HomeService>();

// ---- Centralized error handling ----
builder.Services.AddExceptionHandler<RondiTrackExceptionHandler>();
builder.Services.AddProblemDetails(options =>
    options.CustomizeProblemDetails = context =>
    {
        // 6.3: the framework's own bare errors get the SAME shape as ours: type, title, status, detail, instance, correlationId.
        var problem = context.ProblemDetails;
        problem.Type = $"urn:ronditrack:error:http.{problem.Status}";
        problem.Title = $"http.{problem.Status}";
        problem.Detail ??= "The request could not be completed.";
        problem.Instance ??= context.HttpContext.Request.Path;
        problem.Extensions.Remove("traceId");     // one id only: correlationId
        problem.Extensions["correlationId"] = Correlation.GetId(context.HttpContext);
    });
builder.Services.AddFrontendCors(builder.Configuration);
builder.Services.AddRondiTrackRateLimiting(builder.Configuration);


// ======================= 6.2 OBSERVABILITY: three signals, three questions =======================
//   Logs   answer "what happened to this request?"      (ILogger, as before)
//   Traces answer "where did the time go?"               (one span per request + one per SQL command + our own)
//   Metrics answer "how often, and is it getting worse?" (counters with small, fixed tag sets)
// Everything is labelled "ronditrack-api". Nothing leaves the process unless OTEL_EXPORTER_OTLP_ENDPOINT is set,
// so tests and CI never need a dashboard.


var openTelemetry = builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService("ronditrack-api"))
    .WithTracing(tracing =>
    {
        // One span per incoming request, except the platform's health probes (they would bury real traffic).
        tracing.AddAspNetCoreInstrumentation(options =>
            options.Filter = httpContext => !httpContext.Request.Path.StartsWithSegments("/health"));
        tracing.AddNpgsql();                                  // one child span per SQL command (placeholders, never values)
        tracing.AddSource(RondiTrackTelemetry.ActivitySourceName);   // our own payout.process span
        tracing.AddProcessor(new QueryStringRedactionProcessor());   // URL query strings can carry page tokens (= amounts)
    })
    .WithMetrics(metrics =>
    {
        metrics.AddAspNetCoreInstrumentation();               // request rate, errors, duration
        metrics.AddMeter("System.Runtime");                   // GC, thread pool, CPU (built into .NET)
        metrics.AddMeter(RondiTrackMetrics.MeterName);        // our business counters: a meter nobody registers records into the void
    })
    .WithLogging();                                           // every ILogger call is ALSO sent to OpenTelemetry

// Export over OTLP ONLY when the standard environment variable is set (launchSettings.json sets it for `dotnet run`).
if (!string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
    openTelemetry.UseOtlpExporter();

// ---- Health checks (6.2): liveness runs NO checks, readiness checks PostgreSQL ----
builder.Services.AddHealthChecks()
    .AddDbContextCheck<RondiTrackDbContext>("database", tags: ["ready"]);

// A slow answer is an unhealthy answer: cap every check. Health:DatabaseTimeoutSeconds = 0 removes the cap
// (only for the "before" measurement in the README).
var databaseTimeoutSeconds = builder.Configuration.GetValue("Health:DatabaseTimeoutSeconds", 3);
builder.Services.Configure<HealthCheckServiceOptions>(options =>
{
    foreach (var registration in options.Registrations)
        registration.Timeout = databaseTimeoutSeconds > 0
            ? TimeSpan.FromSeconds(databaseTimeoutSeconds)
            : Timeout.InfiniteTimeSpan;
});

var app = builder.Build();

// ======================= 2. STARTUP WORK =======================
using (var scope = app.Services.CreateScope())
{
    // Apply migrations so a fresh clone (and a fresh test container) gets the schema automatically.
    var db = scope.ServiceProvider.GetRequiredService<RondiTrackDbContext>();
    await db.Database.MigrateAsync();

    // Demo data only in Development, and only when a seed password is configured (user-secrets).
    if (app.Environment.IsDevelopment())
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DevelopmentSeeder");
        await DevelopmentSeeder.ApplyAsync(scope.ServiceProvider, builder.Configuration["SeedUsers:Password"], logger);
    }
}

// ======================= 3. REQUEST PIPELINE (order matters) =======================
app.UseExceptionHandler();   // FIRST: catches anything thrown below and hands it to our handler
app.UseStatusCodePages();    // turns any remaining empty 4xx into problem+json

if (app.Environment.IsDevelopment())
{
    // The fallback policy covers EVERY endpoint, including the docs, so they must opt out.
    app.MapOpenApi().AllowAnonymous();
    app.MapScalarApiReference().AllowAnonymous();
}

app.UseHttpsRedirection();
app.UseCors(CorsPolicies.Frontend); // first thing that looks at the request, so a preflight never reaches authentication 
app.UseAuthentication();   // who are you?
app.UseRateLimiter(); // after authentication (needs the user id), before authorization (which hits the database)
app.UseAuthorization();    // what may you do? (fallback policy + [Authorize])
app.MapControllers();

// The platform has no token, so the probes opt out of the fallback policy. They return ONLY "Healthy" or "Unhealthy".
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false })
    .AllowAnonymous();
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") })
    .AllowAnonymous();

app.Run();

// Makes the auto-generated Program class visible to the test project (WebApplicationFactory<Program>).
public partial class Program
{
}
