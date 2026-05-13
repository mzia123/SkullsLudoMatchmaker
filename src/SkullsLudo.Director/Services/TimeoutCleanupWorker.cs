using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using OpenMatch;
using SkullsLudo.Shared.Configuration;
using SkullsLudo.Shared.Constants;

namespace SkullsLudo.Director.Services;

public sealed class TimeoutCleanupWorker(
    QueryService.QueryServiceClient queryClient,
    BackendService.BackendServiceClient backendClient,
    MatchmakerSettings settings,
    ILogger<TimeoutCleanupWorker> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(10);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Timeout cleanup worker started");

        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CleanupAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Timeout cleanup error");
            }

            await Task.Delay(Interval, stoppingToken);
        }
    }

    private async Task CleanupAsync(CancellationToken ct)
    {
        foreach (var (queueName, queueConfig) in settings.Queues)
        {
            var cutoff = Timestamp.FromDateTime((DateTime.UtcNow - queueConfig.Timeout).ToUniversalTime());

            var pool = new Pool
            {
                Name = $"timeout_scan_{queueName}",
                TagPresentFilters = { new TagPresentFilter { Tag = queueConfig.Tag } },
                CreatedBefore = cutoff
            };

            var timedOut = new List<string>();

            try
            {
                using var stream = queryClient.QueryTickets(
                    new QueryTicketsRequest { Pool = pool }, cancellationToken: ct);

                await foreach (var response in stream.ResponseStream.ReadAllAsync(ct))
                {
                    timedOut.AddRange(response.Tickets.Select(t => t.Id));
                }
            }
            catch (RpcException ex) when (ex.StatusCode == StatusCode.Unavailable)
            {
                continue;
            }

            if (timedOut.Count == 0)
                continue;

            logger.LogInformation("Found {Count} timed-out ticket(s) in {Queue}", timedOut.Count, queueName);

            var timeoutAssignment = new Assignment();
            timeoutAssignment.Extensions[WellKnown.Extensions.TimeoutKey] =
                Any.Pack(new BoolValue { Value = true });

            await backendClient.AssignTicketsAsync(new AssignTicketsRequest
            {
                Assignments =
                {
                    new AssignmentGroup
                    {
                        Assignment = timeoutAssignment,
                        TicketIds = { timedOut }
                    }
                }
            }, cancellationToken: ct);

            logger.LogInformation("Marked {Count} ticket(s) timed out in {Queue}", timedOut.Count, queueName);
        }
    }
}
