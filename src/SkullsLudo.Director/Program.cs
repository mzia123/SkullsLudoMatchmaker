using OpenMatch;
using Serilog;
using SkullsLudo.Director.Services;
using SkullsLudo.Shared.Configuration;
using SkullsLudo.Shared.Constants;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSerilog(loggerConfig =>
    loggerConfig.ReadFrom.Configuration(builder.Configuration));

var matchmakerSettings = builder.Configuration.GetSection(MatchmakerSettings.SectionName)
    .Get<MatchmakerSettings>() ?? BuildDefaultSettings();

builder.Services.AddSingleton(matchmakerSettings);

var omBackendAddress = $"http://{matchmakerSettings.OpenMatch.BackendHost}:{matchmakerSettings.OpenMatch.BackendPort}";
var omQueryAddress = $"http://{matchmakerSettings.OpenMatch.QueryHost}:{matchmakerSettings.OpenMatch.QueryPort}";
var omFrontendAddress = $"http://{matchmakerSettings.OpenMatch.FrontendHost}:{matchmakerSettings.OpenMatch.FrontendPort}";

builder.Services.AddSingleton(_ =>
{
    var channel = Grpc.Net.Client.GrpcChannel.ForAddress(omBackendAddress);
    return new BackendService.BackendServiceClient(channel);
});

builder.Services.AddSingleton(_ =>
{
    var channel = Grpc.Net.Client.GrpcChannel.ForAddress(omQueryAddress);
    return new QueryService.QueryServiceClient(channel);
});

builder.Services.AddSingleton(_ =>
{
    var channel = Grpc.Net.Client.GrpcChannel.ForAddress(omFrontendAddress);
    return new FrontendService.FrontendServiceClient(channel);
});

builder.Services.AddSingleton<IGameServerAllocator>(sp =>
    new AgonesAllocatorService(matchmakerSettings.Agones, sp.GetRequiredService<ILogger<AgonesAllocatorService>>()));

builder.Services.AddHostedService<DirectorWorker>();
builder.Services.AddHostedService<TimeoutCleanupWorker>();

var host = builder.Build();
host.Run();

static MatchmakerSettings BuildDefaultSettings() => new()
{
    OpenMatch = new OpenMatchSettings
    {
        FrontendHost = "open-match-frontend",
        BackendHost = "open-match-backend",
        QueryHost = "open-match-query"
    },
    Agones = new AgonesSettings
    {
        AllocatorHost = "agones-allocator.agones-system.svc"
    },
    MatchFunction = new MatchFunctionSettings
    {
        Host = "skulls-ludo-matchfunction"
    },
    Director = new DirectorSettings(),
    Queues = DefaultQueues.All
};
