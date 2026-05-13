using Grpc.Net.Client;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using OpenMatch;
using Serilog;
using Serilog.Settings.Configuration;
using SkullsLudo.Director.Health;
using SkullsLudo.Director.Services;
using SkullsLudo.Shared.Configuration;

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

var handler = new SocketsHttpHandler { EnableMultipleHttp2Connections = true };

var backendChannel = GrpcChannel.ForAddress(
    $"http://{matchmakerSettings.OpenMatch.BackendHost}:{matchmakerSettings.OpenMatch.BackendPort}",
    new GrpcChannelOptions { HttpHandler = handler });
builder.Services.AddSingleton(new BackendService.BackendServiceClient(backendChannel));

var queryChannel = GrpcChannel.ForAddress(
    $"http://{matchmakerSettings.OpenMatch.QueryHost}:{matchmakerSettings.OpenMatch.QueryPort}",
    new GrpcChannelOptions { HttpHandler = handler });
builder.Services.AddSingleton(new QueryService.QueryServiceClient(queryChannel));

builder.Services.AddSingleton<IGameServerAllocator>(sp =>
    new AgonesAllocatorService(matchmakerSettings.Agones, sp.GetRequiredService<ILogger<AgonesAllocatorService>>()));

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
