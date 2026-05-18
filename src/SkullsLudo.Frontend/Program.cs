using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using OpenMatch;
using Serilog;
using Serilog.Settings.Configuration;
using SkullsLudo.Frontend.Auth;
using SkullsLudo.Frontend.Endpoints;
using SkullsLudo.Frontend.Health;
using SkullsLudo.Frontend.Services;
using SkullsLudo.Shared.Configuration;
using SkullsLudo.Shared.Constants;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddJsonFile(QueueConfigLoader.QueuesFilePath, optional: true, reloadOnChange: false);

var readerOptions = new ConfigurationReaderOptions(typeof(ConsoleLoggerConfigurationExtensions).Assembly);

builder.Host.UseSerilog((context, loggerConfig) =>
    loggerConfig.ReadFrom.Configuration(context.Configuration, readerOptions));

var matchmakerSettings = (builder.Configuration.GetSection(MatchmakerSettings.SectionName)
    .Get<MatchmakerSettings>() ?? new MatchmakerSettings())
    .EnsureQueues();

builder.Services.AddSingleton(matchmakerSettings);

var omFrontendAddress = builder.Configuration.GetValue<string>("Matchmaker:OpenMatch:FrontendAddress")
    ?? $"http://{matchmakerSettings.OpenMatch.FrontendHost}:{matchmakerSettings.OpenMatch.FrontendPort}";

builder.Services.AddGrpcClient<FrontendService.FrontendServiceClient>(o => o.Address = new Uri(omFrontendAddress))
    .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { EnableMultipleHttp2Connections = true });

var omQueryAddress = builder.Configuration.GetValue<string>("Matchmaker:OpenMatch:QueryAddress")
    ?? $"http://{matchmakerSettings.OpenMatch.QueryHost}:{matchmakerSettings.OpenMatch.QueryPort}";

builder.Services.AddGrpcClient<QueryService.QueryServiceClient>(o => o.Address = new Uri(omQueryAddress))
    .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { EnableMultipleHttp2Connections = true });

builder.Services.AddSingleton<IOpenMatchQueryService, OpenMatchQueryService>();

builder.Services.AddSingleton<IOpenMatchFrontendService, OpenMatchFrontendService>();
builder.Services.AddSkullsLudoAuthentication(matchmakerSettings);
builder.Services.AddAuthorization();

builder.Services.AddSingleton<OpenMatchFrontendTcpHealthCheck>();
builder.Services.AddHealthChecks()
    .AddCheck("live", () => HealthCheckResult.Healthy(), tags: ["live"])
    .AddCheck<OpenMatchFrontendTcpHealthCheck>("open-match-frontend", tags: ["ready"]);

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

if (!matchmakerSettings.UnityAuth.Enabled)
{
    app.Logger.LogWarning(
        "Unity Authentication is DISABLED. Player id from {Header} or default {DefaultId}. Do not use in production.",
        matchmakerSettings.UnityAuth.DebugPlayerIdHeader,
        matchmakerSettings.UnityAuth.DefaultDebugPlayerId
        );
}

app.UseSerilogRequestLogging();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.MapOpenApi();
app.MapHealthChecks("/healthz/live", new HealthCheckOptions { Predicate = r => r.Tags.Contains("live") });
app.MapHealthChecks("/healthz/ready", new HealthCheckOptions { Predicate = r => r.Tags.Contains("ready") });
app.MapMatchmakingEndpoints();

app.Run();