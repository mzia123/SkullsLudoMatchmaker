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
    private const int CleanupIntervalMs = 10_000;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Timeout cleanup worker started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CleanupTimedOutTicketsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error during timeout cleanup");
            }

            await Task.Delay(CleanupIntervalMs, stoppingToken);
        }
    }

    private async Task CleanupTimedOutTicketsAsync(CancellationToken ct)
    {
        foreach (var (queueName, queueConfig) in settings.Queues)
        {
            var cutoff = DateTime.UtcNow - queueConfig.Timeout;

            var pool = new Pool
            {
                Name = $"timeout_scan_{queueName}",
                TagPresentFilters = { new TagPresentFilter { Tag = queueConfig.Tag } },
                CreatedBefore = Timestamp.FromDateTime(cutoff.ToUniversalTime())
            };

            var timedOutTickets = new List<string>();

            try
            {
                using var stream = queryClient.QueryTickets(
                    new QueryTicketsRequest { Pool = pool },
                    cancellationToken: ct);

                await foreach (var response in stream.ResponseStream.ReadAllAsync(ct))
                {
                    foreach (var ticket in response.Tickets)
                    {
                        timedOutTickets.Add(ticket.Id);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error querying timed-out tickets for queue {Queue}", queueName);
                continue;
            }

            if (timedOutTickets.Count == 0)
                continue;

            logger.LogInformation("Found {Count} timed-out ticket(s) in queue {Queue}", timedOutTickets.Count, queueName);

            var timeoutAssignment = new Assignment();
            timeoutAssignment.Extensions[WellKnown.Extensions.TimeoutKey] =
                Any.Pack(new BoolValue { Value = true });

            var assignRequest = new AssignTicketsRequest
            {
                Assignments =
                {
                    new AssignmentGroup
                    {
                        Assignment = timeoutAssignment,
                        TicketIds = { timedOutTickets }
                    }
                }
            };

            try
            {
                await backendClient.AssignTicketsAsync(assignRequest, cancellationToken: ct);
                logger.LogInformation("Marked {Count} ticket(s) as timed out in queue {Queue}",
                    timedOutTickets.Count, queueName);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error assigning timeout to tickets in queue {Queue}", queueName);
            }
        }
    }
}
