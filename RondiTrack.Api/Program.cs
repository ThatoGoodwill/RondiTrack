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
using RondiTrack.Api.Jobs;

var builder = WebApplication.CreateBuilder(args);

// ======================= 1. REGISTER SERVICES (dependency injection) =======================

builder.Services.AddControllers(options =>
{
    options.Filters.Add<StokvelScopeFilter>();
    options.Filters.Add<ValidationFilter>();
})
.AddJsonOptions(o => o.JsonSerializerOptions.NumberHandling = JsonNumberHandling.Strict);

builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.NumberHandling = JsonNumberHandling.Strict);

builder.Services.Configure<ApiBehaviorOptions>(options => options.SuppressModelStateInvalidFilter = true);
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

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

var connectionString = builder.Configuration.GetConnectionString("RondiTrack")
    ?? throw new InvalidOperationException(
        "Setting 'ConnectionStrings__RondiTrack' is missing. " +
        "In Docker or on a server set it as the environment variable ConnectionStrings__RondiTrack. " +
        "For local development: dotnet user-secrets set \"ConnectionStrings:RondiTrack\" \"<your connection string>\" --project RondiTrack.Api");

var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
jwtOptions.EnsureValid();
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;   

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtOptions.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],   
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),                
            NameClaimType = JwtRegisteredClaimNames.Sub,
            RoleClaimType = AppClaims.Role
        };

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
                AuthorizationRefusals.ReportForbidden(context.HttpContext);
                return ProblemResponseWriter.WriteAsync(context.HttpContext,
                    StatusCodes.Status403Forbidden, "auth.forbidden", "You do not have permission to perform this action.");
            }
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();

    options.AddPolicy(Policies.PlatformAdmin, p => p.RequireRole(PlatformRoles.PlatformAdmin));

    options.AddPolicy(Policies.StokvelMember,
        p => p.AddRequirements(new StokvelRoleRequirement(StokvelMemberRole.Member)));
    options.AddPolicy(Policies.StokvelTreasurer,
        p => p.AddRequirements(new StokvelRoleRequirement(StokvelMemberRole.Treasurer)));
    options.AddPolicy(Policies.StokvelChairperson,
        p => p.AddRequirements(new StokvelRoleRequirement(StokvelMemberRole.Admin)));
    options.AddPolicy(Policies.RecordContribution,
        p => p.AddRequirements(new RecordContributionRequirement()));
});

builder.Services.AddScoped<IAuthorizationHandler, StokvelRoleHandler>();
builder.Services.AddScoped<IAuthorizationHandler, RecordContributionHandler>();
builder.Services.AddScoped<StokvelAccessChecker>();

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<RondiTrackMetrics>();
builder.Services.AddSingleton<TokenService>();
builder.Services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();

builder.Services.AddDbContext<RondiTrackDbContext>(options => options.UseNpgsql(connectionString));

builder.Services.AddScoped<IUserRepository, EfUserRepository>();
builder.Services.AddScoped<IStokvelRepository, EfStokvelRepository>();
builder.Services.AddScoped<IStokvelMemberRepository, SqlStokvelMemberRepository>();
builder.Services.AddScoped<IContributionRepository, EfContributionRepository>();
builder.Services.AddScoped<IContributionCycleRepository, EfContributionCycleRepository>();
builder.Services.AddScoped<IRefreshTokenRepository, EfRefreshTokenRepository>();
builder.Services.AddScoped<IIdempotencyStore, EfIdempotencyStore>();

builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IMembershipService, MembershipService>();
builder.Services.AddScoped<ContributionService>();
builder.Services.AddScoped<IContributionService>(sp => new InstrumentedContributionService(
    sp.GetRequiredService<ContributionService>(), sp.GetRequiredService<RondiTrackMetrics>()));
builder.Services.AddScoped<PayoutService>();
builder.Services.AddScoped<IPayoutService>(sp => new InstrumentedPayoutService(
    sp.GetRequiredService<PayoutService>(), sp.GetRequiredService<RondiTrackMetrics>()));
builder.Services.AddScoped<AuthService>();

var cycleCloseOptions = builder.Configuration.GetSection(CycleCloseOptions.SectionName).Get<CycleCloseOptions>() ?? new CycleCloseOptions();
cycleCloseOptions.EnsureValid();
builder.Services.Configure<CycleCloseOptions>(builder.Configuration.GetSection(CycleCloseOptions.SectionName));

var housekeepingOptions = builder.Configuration.GetSection(HousekeepingOptions.SectionName).Get<HousekeepingOptions>() ?? new HousekeepingOptions();
housekeepingOptions.EnsureValid();
builder.Services.Configure<HousekeepingOptions>(builder.Configuration.GetSection(HousekeepingOptions.SectionName));

builder.Services.AddSingleton<JobMetrics>();
builder.Services.AddSingleton<CycleCloseJob>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<CycleCloseJob>());
builder.Services.AddSingleton<HousekeepingJob>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<HousekeepingJob>());
builder.Services.AddScoped<IHomeService, HomeService>();

builder.Services.AddExceptionHandler<RondiTrackExceptionHandler>();
builder.Services.AddProblemDetails(options =>
    options.CustomizeProblemDetails = context =>
    {
        var problem = context.ProblemDetails;
        problem.Type = $"urn:ronditrack:error:http.{problem.Status}";
        problem.Title = $"http.{problem.Status}";
        problem.Detail ??= "The request could not be completed.";
        problem.Instance ??= context.HttpContext.Request.Path;
        problem.Extensions.Remove("traceId");     
        problem.Extensions["correlationId"] = Correlation.GetId(context.HttpContext);
    });

builder.Services.AddFrontendCors(builder.Configuration);
builder.Services.AddRondiTrackRateLimiting(builder.Configuration);

var openTelemetry = builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService("ronditrack-api"))
    .WithTracing(tracing =>
    {
        tracing.AddAspNetCoreInstrumentation(options =>
            options.Filter = httpContext => !httpContext.Request.Path.StartsWithSegments("/health"));
        tracing.AddNpgsql();                                  
        tracing.AddSource(RondiTrackTelemetry.ActivitySourceName);   
        tracing.AddProcessor(new QueryStringRedactionProcessor());   
    })
    .WithMetrics(metrics =>
    {
        metrics.AddAspNetCoreInstrumentation();               
        metrics.AddMeter("System.Runtime");                   
        metrics.AddMeter(RondiTrackMetrics.MeterName);        
    })
    .WithLogging();                                           

if (!string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
    openTelemetry.UseOtlpExporter();

builder.Services.AddHealthChecks()
    .AddDbContextCheck<RondiTrackDbContext>("database", tags: ["ready"]);

var databaseTimeoutSeconds = builder.Configuration.GetValue("Health:DatabaseTimeoutSeconds", 3);
builder.Services.Configure<HealthCheckServiceOptions>(options =>
{
    foreach (var registration in options.Registrations)
        registration.Timeout = databaseTimeoutSeconds > 0
            ? TimeSpan.FromSeconds(databaseTimeoutSeconds)
            : Timeout.InfiniteTimeSpan;
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<RondiTrackDbContext>();
    await db.Database.MigrateAsync();

    if (app.Environment.IsDevelopment())
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DevelopmentSeeder");
        await DevelopmentSeeder.ApplyAsync(scope.ServiceProvider, builder.Configuration["SeedUsers:Password"], logger);
    }
}

// ======================= 3. REQUEST PIPELINE (order matters) =======================
app.UseExceptionHandler();   
app.UseStatusCodePages();    

// 🚨 CORS PREFLIGHT SHORTCUT: intercept OPTIONS before the fallback policy can 401 it.
app.Use(async (context, next) =>
{
    if (context.Request.Method == "OPTIONS" && context.Request.Headers.ContainsKey("Origin"))
    {
        var origin = context.Request.Headers["Origin"].ToString();
        var allowed = new[] {
            "http://localhost:3000",
            "https://upgraded-computing-machine-97qj9prj9p64h75q-3000.app.github.dev"
        };
        if (allowed.Contains(origin))
        {
            context.Response.StatusCode = 204;
            context.Response.Headers["Access-Control-Allow-Origin"] = origin;
            context.Response.Headers["Access-Control-Allow-Methods"] = "GET, POST, PUT, DELETE, OPTIONS";
            context.Response.Headers["Access-Control-Allow-Headers"] =
                context.Request.Headers["Access-Control-Request-Headers"].ToString();
            context.Response.Headers["Access-Control-Allow-Credentials"] = "true";
            context.Response.Headers["Access-Control-Max-Age"] = "600";
            return;
        }
    }
    await next();
});

// 1. CORS for actual requests (OPTIONS already handled above)
app.UseCors(CorsPolicies.Frontend);

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
    app.MapScalarApiReference().AllowAnonymous();
}

app.UseHttpsRedirection();

app.UseAuthentication();   
app.UseRateLimiter();      
app.UseAuthorization();    

app.MapControllers().RequireCors(CorsPolicies.Frontend);

app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false })
    .AllowAnonymous();
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") })
    .AllowAnonymous();

app.Run();

public partial class Program
{
}