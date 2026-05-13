using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Diagnostics.HealthChecks;
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
    options.ListenAnyIP(WellKnown.Ports.Evaluator, o => o.Protocols = HttpProtocols.Http2);
    options.ListenAnyIP(8080, o => o.Protocols = HttpProtocols.Http1);
});

builder.Services.AddGrpc();
builder.Services.AddHealthChecks()
    .AddCheck("live", () => HealthCheckResult.Healthy(), tags: ["live", "ready"]);

builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));
builder.Services.AddOpenApi();

var app = builder.Build();

app.UseCors();
app.MapOpenApi();
app.MapGrpcService<EvaluatorService>();
app.MapHealthChecks("/healthz/live", new HealthCheckOptions { Predicate = r => r.Tags.Contains("live") });
app.MapHealthChecks("/healthz/ready", new HealthCheckOptions { Predicate = r => r.Tags.Contains("ready") });

app.Run();
