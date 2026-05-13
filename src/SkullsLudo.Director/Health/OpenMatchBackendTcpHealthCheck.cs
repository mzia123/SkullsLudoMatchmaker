using System.Net.Sockets;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using SkullsLudo.Shared.Configuration;

namespace SkullsLudo.Director.Health;

/// <summary>Readiness: TCP connect to Open Match Backend (gRPC port).</summary>
public sealed class OpenMatchBackendTcpHealthCheck(MatchmakerSettings settings) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var tcp = new TcpClient();
            await tcp.ConnectAsync(settings.OpenMatch.BackendHost, settings.OpenMatch.BackendPort, cancellationToken)
                .ConfigureAwait(false);
            return HealthCheckResult.Healthy();
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Cannot reach Open Match Backend", ex);
        }
    }
}
