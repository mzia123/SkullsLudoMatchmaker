using System.Net.Sockets;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using SkullsLudo.Shared.Configuration;

namespace SkullsLudo.MatchFunction.Health;

public sealed class OpenMatchQueryTcpHealthCheck(MatchmakerSettings settings) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var tcp = new TcpClient();
            await tcp.ConnectAsync(settings.OpenMatch.QueryHost, settings.OpenMatch.QueryPort, cancellationToken)
                .ConfigureAwait(false);
            return HealthCheckResult.Healthy();
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Cannot reach Open Match Query", ex);
        }
    }
}
