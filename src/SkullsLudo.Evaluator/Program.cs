using Serilog;
using Serilog.Settings.Configuration;
using SkullsLudo.Evaluator.Services;
using SkullsLudo.Shared.Constants;

var builder = WebApplication.CreateBuilder(args);

var readerOptions = new ConfigurationReaderOptions(typeof(ConsoleLoggerConfigurationExtensions).Assembly);
builder.Host.UseSerilog((context, loggerConfig) =>
    loggerConfig.ReadFrom.Configuration(context.Configuration, readerOptions));

builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(WellKnown.Ports.Evaluator, o => o.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http2);
    options.ListenAnyIP(8080, o => o.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1);
});

builder.Services.AddGrpc();
builder.Services.AddHealthChecks();

var app = builder.Build();

app.MapGrpcService<EvaluatorService>();
app.MapHealthChecks("/healthz");

app.Run();
