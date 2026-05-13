using Grpc.Net.Client;
using OpenMatch;
using Serilog;
using Serilog.Settings.Configuration;
using SkullsLudo.MatchFunction.Services;
using SkullsLudo.MatchFunction.Strategies;
using SkullsLudo.Shared.Configuration;
using SkullsLudo.Shared.Constants;

var builder = WebApplication.CreateBuilder(args);

// Shared queue catalogue from the skulls-ludo-queues ConfigMap (optional).
builder.Configuration.AddJsonFile(QueueConfigLoader.QueuesFilePath, optional: true, reloadOnChange: true);

var readerOptions = new ConfigurationReaderOptions(typeof(ConsoleLoggerConfigurationExtensions).Assembly);
builder.Host.UseSerilog((context, loggerConfig) =>
    loggerConfig.ReadFrom.Configuration(context.Configuration, readerOptions));

builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(WellKnown.Ports.MatchFunction, o =>
        o.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http2);
    options.ListenAnyIP(8080, o =>
        o.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1);
});

builder.Services.AddGrpc();

var omQueryAddress = builder.Configuration.GetValue<string>("Matchmaker:OpenMatch:QueryAddress")
    ?? $"http://{builder.Configuration.GetValue("Matchmaker:OpenMatch:QueryHost", "open-match-query")}:{WellKnown.Ports.OpenMatchQuery}";

builder.Services.AddSingleton(_ =>
{
    var channel = GrpcChannel.ForAddress(omQueryAddress, new GrpcChannelOptions
    {
        HttpHandler = new SocketsHttpHandler { EnableMultipleHttp2Connections = true }
    });
    return new QueryService.QueryServiceClient(channel);
});

var matchmakerSettings = (builder.Configuration.GetSection(MatchmakerSettings.SectionName)
    .Get<MatchmakerSettings>() ?? new MatchmakerSettings())
    .EnsureQueues();

builder.Services.AddSingleton(matchmakerSettings);

// Strategies are stateless and queue-agnostic. Queues bind to a strategy by name
// via QueueConfiguration.Strategy, so one strategy can serve many queues.
builder.Services.AddSingleton<IMatchStrategy, SoloMatchStrategy>();
builder.Services.AddSingleton<IMatchStrategy, DegradingMmrMatchStrategy>();
builder.Services.AddSingleton<MatchStrategyResolver>();
builder.Services.AddHealthChecks();

var app = builder.Build();

app.MapGrpcService<MatchFunctionService>();
app.MapHealthChecks("/healthz");

app.Run();
