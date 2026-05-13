using System.Net.Sockets;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using SkullsLudo.Shared.Configuration;

namespace SkullsLudo.Frontend.Health;

public sealed class OpenMatchFrontendTcpHealthCheck(MatchmakerSettings settings) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var tcp = new TcpClient();
            await tcp.ConnectAsync(settings.OpenMatch.FrontendHost, settings.OpenMatch.FrontendPort, cancellationToken)
                .ConfigureAwait(false);
            return HealthCheckResult.Healthy();
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Cannot reach Open Match Frontend", ex);
        }
    }
}
