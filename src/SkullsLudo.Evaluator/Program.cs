using Serilog;
using SkullsLudo.Evaluator.Services;
using SkullsLudo.Shared.Constants;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, loggerConfig) =>
    loggerConfig.ReadFrom.Configuration(context.Configuration));

builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(WellKnown.Ports.Evaluator, o => o.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1AndHttp2);
});

builder.Services.AddGrpc();
builder.Services.AddHealthChecks();

var app = builder.Build();

app.MapGrpcService<EvaluatorService>();
app.MapHealthChecks("/healthz");

app.Run();
