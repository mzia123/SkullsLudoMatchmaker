using System.Net.Http;
using Grpc.Net.Client;
using OpenMatch;
using Serilog;
using Serilog.Settings.Configuration;
using SkullsLudo.Director.Services;
using SkullsLudo.Shared.Configuration;

var builder = Host.CreateApplicationBuilder(args);

var readerOptions = new ConfigurationReaderOptions(typeof(ConsoleLoggerConfigurationExtensions).Assembly);
builder.Services.AddSerilog(loggerConfig =>
    loggerConfig.ReadFrom.Configuration(builder.Configuration, readerOptions));

var matchmakerSettings = builder.Configuration.GetSection(MatchmakerSettings.SectionName)
    .Get<MatchmakerSettings>() ?? new MatchmakerSettings();

builder.Services.AddSingleton(matchmakerSettings);

var omBackendAddress = $"http://{matchmakerSettings.OpenMatch.BackendHost}:{matchmakerSettings.OpenMatch.BackendPort}";
var omQueryAddress = $"http://{matchmakerSettings.OpenMatch.QueryHost}:{matchmakerSettings.OpenMatch.QueryPort}";
var omFrontendAddress = $"http://{matchmakerSettings.OpenMatch.FrontendHost}:{matchmakerSettings.OpenMatch.FrontendPort}";

var backendChannel = GrpcChannel.ForAddress(omBackendAddress, new GrpcChannelOptions
{
    HttpHandler = new SocketsHttpHandler { EnableMultipleHttp2Connections = true }
});
builder.Services.AddSingleton(new BackendService.BackendServiceClient(backendChannel));

var queryChannel = GrpcChannel.ForAddress(omQueryAddress, new GrpcChannelOptions
{
    HttpHandler = new SocketsHttpHandler { EnableMultipleHttp2Connections = true }
});
builder.Services.AddSingleton(new QueryService.QueryServiceClient(queryChannel));

var frontendChannel = GrpcChannel.ForAddress(omFrontendAddress, new GrpcChannelOptions
{
    HttpHandler = new SocketsHttpHandler { EnableMultipleHttp2Connections = true }
});
builder.Services.AddSingleton(new FrontendService.FrontendServiceClient(frontendChannel));

builder.Services.AddSingleton<IGameServerAllocator>(sp =>
    new AgonesAllocatorService(matchmakerSettings.Agones, sp.GetRequiredService<ILogger<AgonesAllocatorService>>()));

builder.Services.AddHostedService<DirectorWorker>();
builder.Services.AddHostedService<TimeoutCleanupWorker>();

var host = builder.Build();
host.Run();
