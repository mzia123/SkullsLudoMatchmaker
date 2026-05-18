using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using OpenMatch;
using Serilog;
using Serilog.Settings.Configuration;
using SkullsLudo.MatchFunction.Health;
using SkullsLudo.MatchFunction.Services;
using SkullsLudo.MatchFunction.Strategies;
using SkullsLudo.Shared.Configuration;
using SkullsLudo.Shared.Constants;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddJsonFile(QueueConfigLoader.QueuesFilePath, optional: true, reloadOnChange: false);

var readerOptions = new ConfigurationReaderOptions(typeof(ConsoleLoggerConfigurationExtensions).Assembly);
builder.Host.UseSerilog((context, loggerConfig) =>
    loggerConfig.ReadFrom.Configuration(context.Configuration, readerOptions));

builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(WellKnown.Ports.MatchFunction, o =>
        o.Protocols = HttpProtocols.Http2);
    options.ListenAnyIP(8080, o =>
        o.Protocols = HttpProtocols.Http1);
});

builder.Services.AddGrpc();

var omQueryAddress = builder.Configuration.GetValue<string>("Matchmaker:OpenMatch:QueryAddress")
    ?? $"http://{builder.Configuration.GetValue("Matchmaker:OpenMatch:QueryHost", "open-match-query")}:{WellKnown.Ports.OpenMatchQuery}";

builder.Services.AddGrpcClient<QueryService.QueryServiceClient>(o => o.Address = new Uri(omQueryAddress))
    .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { EnableMultipleHttp2Connections = true });

var matchmakerSettings = (builder.Configuration.GetSection(MatchmakerSettings.SectionName)
    .Get<MatchmakerSettings>() ?? new MatchmakerSettings())
    .EnsureQueues();

builder.Services.AddSingleton(matchmakerSettings);

builder.Services.AddSingleton<IMatchStrategy, SoloMatchStrategy>();
builder.Services.AddSingleton<IMatchStrategy, DegradingMmrMatchStrategy>();
builder.Services.AddSingleton<MatchStrategyResolver>();

builder.Services.AddSingleton<OpenMatchQueryTcpHealthCheck>();
builder.Services.AddHealthChecks()
    .AddCheck("live", () => HealthCheckResult.Healthy(), tags: ["live"])
    .AddCheck<OpenMatchQueryTcpHealthCheck>("open-match-query", tags: ["ready"]);

builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));
builder.Services.AddOpenApi();

var app = builder.Build();

app.UseCors();
app.MapOpenApi();
app.MapGrpcService<MatchFunctionService>();
app.MapHealthChecks("/healthz/live", new HealthCheckOptions { Predicate = r => r.Tags.Contains("live") });
app.MapHealthChecks("/healthz/ready", new HealthCheckOptions { Predicate = r => r.Tags.Contains("ready") });

app.Run();
