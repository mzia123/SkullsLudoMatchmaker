using Grpc.Net.Client;
using OpenMatch;
using Serilog;
using Serilog.Settings.Configuration;
using SkullsLudo.Director.Services;
using SkullsLudo.Shared.Configuration;

var builder = Host.CreateApplicationBuilder(args);

// Shared queue catalogue from the skulls-ludo-queues ConfigMap. Optional: if absent
// (ConfigMap not deployed), EnsureQueues() below falls back to DefaultQueues.All.
builder.Configuration.AddJsonFile(QueueConfigLoader.QueuesFilePath, optional: true, reloadOnChange: true);

var readerOptions = new ConfigurationReaderOptions(typeof(ConsoleLoggerConfigurationExtensions).Assembly);
builder.Services.AddSerilog(loggerConfig =>
    loggerConfig.ReadFrom.Configuration(builder.Configuration, readerOptions));

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

builder.Services.AddHostedService<DirectorWorker>();
builder.Services.AddHostedService<TimeoutCleanupWorker>();

var host = builder.Build();
host.Run();
