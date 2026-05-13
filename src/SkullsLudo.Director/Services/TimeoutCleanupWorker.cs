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
    private static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(10);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Timeout cleanup worker started");

        await Task.Delay(StartupDelay, stoppingToken);

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
            // Safety net for tickets whose client stopped polling. The Frontend already enforces the
            // per-queue timeout for live polling, so the small race window here (a ticket may get matched
            // between query and stamp) is acceptable; transient RPC errors are caught per-queue and the
            // next 10 s cycle retries.
            try
            {
                await ProcessQueueAsync(queueName, queueConfig, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (RpcException ex)
            {
                logger.LogWarning("Cleanup gRPC error on queue {Queue} ({Code}); retrying next cycle",
                    queueName, ex.StatusCode);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Cleanup error on queue {Queue}", queueName);
            }
        }
    }

    private async Task ProcessQueueAsync(string queueName, QueueConfiguration queueConfig, CancellationToken ct)
    {
        var cutoff = Timestamp.FromDateTime((DateTime.UtcNow - queueConfig.Timeout).ToUniversalTime());

        var pool = new Pool
        {
            Name = $"timeout_scan_{queueName}",
            TagPresentFilters = { new TagPresentFilter { Tag = queueConfig.Tag } },
            CreatedBefore = cutoff
        };

        var timedOut = new List<string>();

        using (var stream = queryClient.QueryTickets(new QueryTicketsRequest { Pool = pool }, cancellationToken: ct))
        {
            await foreach (var response in stream.ResponseStream.ReadAllAsync(ct))
            {
                timedOut.AddRange(response.Tickets.Select(t => t.Id));
            }
        }

        if (timedOut.Count == 0)
            return;

        var timeoutAssignment = new Assignment();
        timeoutAssignment.Extensions[WellKnown.Extensions.TimeoutKey] =
            Any.Pack(new BoolValue { Value = true });

        var assign = await backendClient.AssignTicketsAsync(new AssignTicketsRequest
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

        var stamped = timedOut.Count - assign.Failures.Count;
        logger.LogInformation("Marked {Stamped}/{Total} ticket(s) timed out in {Queue}",
            stamped, timedOut.Count, queueName);

        // Failures are typically TICKET_NOT_FOUND (the player canceled or the Frontend deleted the
        // ticket between query and stamp). Debug-level so a busy queue doesn't spam Info.
        foreach (var failure in assign.Failures)
            logger.LogDebug("Cleanup assign skipped ticket {TicketId}: {Cause}", failure.TicketId, failure.Cause);
    }
}
