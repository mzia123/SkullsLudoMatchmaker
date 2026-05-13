using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using OpenMatch;
using Serilog;
using Serilog.Settings.Configuration;
using SkullsLudo.Frontend.Endpoints;
using SkullsLudo.Frontend.Health;
using SkullsLudo.Frontend.Services;
using SkullsLudo.Shared.Configuration;
using SkullsLudo.Shared.Constants;

var builder = WebApplication.CreateBuilder(args);

// Queue catalogue from ConfigMap (optional). Loaded once at startup with MatchmakerSettings; rollout to pick up edits.
builder.Configuration.AddJsonFile(QueueConfigLoader.QueuesFilePath, optional: true, reloadOnChange: false);

var readerOptions = new ConfigurationReaderOptions(typeof(ConsoleLoggerConfigurationExtensions).Assembly);
builder.Host.UseSerilog((context, loggerConfig) =>
    loggerConfig.ReadFrom.Configuration(context.Configuration, readerOptions));

var omFrontendAddress = builder.Configuration.GetValue<string>("Matchmaker:OpenMatch:FrontendAddress")
    ?? $"http://{builder.Configuration.GetValue("Matchmaker:OpenMatch:FrontendHost", "open-match-frontend")}:{WellKnown.Ports.OpenMatchFrontend}";

builder.Services.AddGrpcClient<FrontendService.FrontendServiceClient>(o =>
    o.Address = new Uri(omFrontendAddress));

var matchmakerSettings = (builder.Configuration.GetSection(MatchmakerSettings.SectionName)
    .Get<MatchmakerSettings>() ?? new MatchmakerSettings())
    .EnsureQueues();

builder.Services.AddSingleton(matchmakerSettings);

builder.Services.AddSingleton<IOpenMatchFrontendService, OpenMatchFrontendService>();
builder.Services.AddMemoryCache();

builder.Services.AddSingleton<OpenMatchFrontendTcpHealthCheck>();
builder.Services.AddHealthChecks()
    .AddCheck("live", () => HealthCheckResult.Healthy(), tags: ["live"])
    .AddCheck<OpenMatchFrontendTcpHealthCheck>("open-match-frontend", tags: ["ready"]);

// Per-IP fixed-window limiter for POST /tickets. Behind a LoadBalancer the source IP must be the
// real client (e.g. Service.spec.externalTrafficPolicy: Local, or a proxy that sets X-Forwarded-For).
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy(MatchmakingEndpoints.CreateTicketRateLimitPolicy, context =>
    {
        var key = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
        {
            Window = TimeSpan.FromSeconds(matchmakerSettings.Frontend.CreateTicketRateLimitWindowSeconds),
            PermitLimit = matchmakerSettings.Frontend.CreateTicketRateLimitPermits,
            QueueLimit = 0
        });
    });
});

builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));
builder.Services.AddOpenApi();

var app = builder.Build();

app.UseSerilogRequestLogging();
app.UseCors();
app.UseRateLimiter();
app.MapOpenApi();
app.MapHealthChecks("/healthz/live", new HealthCheckOptions { Predicate = r => r.Tags.Contains("live") });
app.MapHealthChecks("/healthz/ready", new HealthCheckOptions { Predicate = r => r.Tags.Contains("ready") });
app.MapMatchmakingEndpoints();

app.Run();
