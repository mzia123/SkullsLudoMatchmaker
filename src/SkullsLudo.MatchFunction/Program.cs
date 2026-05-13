using Grpc.Net.Client;
using OpenMatch;
using Serilog;
using Serilog.Settings.Configuration;
using SkullsLudo.MatchFunction.Services;
using SkullsLudo.MatchFunction.Strategies;
using SkullsLudo.Shared.Configuration;
using SkullsLudo.Shared.Constants;

var builder = WebApplication.CreateBuilder(args);

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

var queues = builder.Configuration.GetSection("Matchmaker:Queues")
    .Get<Dictionary<string, QueueConfiguration>>() ?? DefaultQueues.All;

builder.Services.AddSingleton<IMatchStrategy>(new PracticeMatchStrategy());
builder.Services.AddSingleton<IMatchStrategy>(new QuickplayMatchStrategy(
    queues.GetValueOrDefault(WellKnown.Queues.Quickplay, DefaultQueues.Quickplay)));
builder.Services.AddSingleton<MatchStrategyResolver>();
builder.Services.AddHealthChecks();

var app = builder.Build();

app.MapGrpcService<MatchFunctionService>();
app.MapHealthChecks("/healthz");

app.Run();
