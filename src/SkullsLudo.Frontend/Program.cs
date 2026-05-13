using OpenMatch;
using Serilog;
using Serilog.Settings.Configuration;
using SkullsLudo.Frontend.Endpoints;
using SkullsLudo.Frontend.Services;
using SkullsLudo.Shared.Constants;

var builder = WebApplication.CreateBuilder(args);

var readerOptions = new ConfigurationReaderOptions(typeof(ConsoleLoggerConfigurationExtensions).Assembly);
builder.Host.UseSerilog((context, loggerConfig) =>
    loggerConfig.ReadFrom.Configuration(context.Configuration, readerOptions));

var omFrontendAddress = builder.Configuration.GetValue<string>("Matchmaker:OpenMatch:FrontendAddress")
    ?? $"http://{builder.Configuration.GetValue("Matchmaker:OpenMatch:FrontendHost", "open-match-frontend")}:{WellKnown.Ports.OpenMatchFrontend}";

builder.Services.AddGrpcClient<FrontendService.FrontendServiceClient>(o =>
    o.Address = new Uri(omFrontendAddress));

builder.Services.AddSingleton<IOpenMatchFrontendService, OpenMatchFrontendService>();
builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseSerilogRequestLogging();
app.MapHealthChecks("/healthz");
app.MapMatchmakingEndpoints();

app.Run();
