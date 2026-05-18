using Agones.Allocation;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using OpenMatch;
using Serilog;
using Serilog.Settings.Configuration;
using SkullsLudo.Director.Health;
using SkullsLudo.Director.Services;
using SkullsLudo.Shared.Configuration;
using SkullsLudo.Shared.Constants;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddJsonFile(QueueConfigLoader.QueuesFilePath, optional: true, reloadOnChange: false);

var readerOptions = new ConfigurationReaderOptions(typeof(ConsoleLoggerConfigurationExtensions).Assembly);
builder.Host.UseSerilog((context, loggerConfig) =>
    loggerConfig.ReadFrom.Configuration(context.Configuration, readerOptions));

builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(8080, o => o.Protocols = HttpProtocols.Http1);
});

var matchmakerSettings = (builder.Configuration.GetSection(MatchmakerSettings.SectionName)
    .Get<MatchmakerSettings>() ?? new MatchmakerSettings())
    .EnsureQueues();

builder.Services.AddSingleton(matchmakerSettings);

var omBackendAddress = builder.Configuration.GetValue<string>("Matchmaker:OpenMatch:BackendAddress")
    ?? $"http://{matchmakerSettings.OpenMatch.BackendHost}:{matchmakerSettings.OpenMatch.BackendPort}";

var omQueryAddress = builder.Configuration.GetValue<string>("Matchmaker:OpenMatch:QueryAddress")
    ?? $"http://{matchmakerSettings.OpenMatch.QueryHost}:{matchmakerSettings.OpenMatch.QueryPort}";

builder.Services.AddGrpcClient<BackendService.BackendServiceClient>(o => o.Address = new Uri(omBackendAddress))
    .ConfigurePrimaryHttpMessageHandler(CreateOpenMatchHandler);

builder.Services.AddGrpcClient<QueryService.QueryServiceClient>(o => o.Address = new Uri(omQueryAddress))
    .ConfigurePrimaryHttpMessageHandler(CreateOpenMatchHandler);

builder.Services.AddGrpcClient<AllocationService.AllocationServiceClient>((sp, o) =>
{
    var agones = sp.GetRequiredService<MatchmakerSettings>().Agones;
    o.Address = new Uri($"https://{agones.AllocatorHost}:{agones.AllocatorPort}");
})
.ConfigurePrimaryHttpMessageHandler(sp =>
{
    var settings = sp.GetRequiredService<MatchmakerSettings>().Agones;
    var logger = sp.GetRequiredService<ILogger<AgonesAllocatorService>>();
    return AgonesAllocatorService.CreateHttpHandler(settings, logger);
});

builder.Services.AddSingleton<IGameServerAllocator, AgonesAllocatorService>();

builder.Services.AddSingleton<OpenMatchBackendTcpHealthCheck>();
builder.Services.AddHealthChecks()
    .AddCheck("live", () => HealthCheckResult.Healthy(), tags: ["live"])
    .AddCheck<OpenMatchBackendTcpHealthCheck>("open-match-backend", tags: ["ready"]);

builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));
builder.Services.AddOpenApi();

builder.Services.AddHostedService<DirectorWorker>();
builder.Services.AddHostedService<TimeoutCleanupWorker>();

var app = builder.Build();

app.UseCors();
app.MapOpenApi();
app.MapHealthChecks("/healthz/live", new HealthCheckOptions { Predicate = r => r.Tags.Contains("live") });
app.MapHealthChecks("/healthz/ready", new HealthCheckOptions { Predicate = r => r.Tags.Contains("ready") });

await app.RunAsync();

static SocketsHttpHandler CreateOpenMatchHandler() =>
    new() { EnableMultipleHttp2Connections = true };
