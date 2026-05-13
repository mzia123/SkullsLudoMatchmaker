using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using OpenMatch;
using SkullsLudo.Shared.Configuration;
using SkullsLudo.Shared.Constants;

namespace SkullsLudo.Director.Services;

public sealed class DirectorWorker(
    BackendService.BackendServiceClient backendClient,
    IGameServerAllocator allocator,
    MatchmakerSettings settings,
    ILogger<DirectorWorker> logger) : BackgroundService
{
    private static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Director starting in {Delay}s. Loop interval: {Interval}ms",
            StartupDelay.TotalSeconds, settings.Director.LoopIntervalMs);

        await Task.Delay(StartupDelay, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunMatchCycleAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in match cycle");
            }

            await Task.Delay(settings.Director.LoopIntervalMs, stoppingToken);
        }

        logger.LogInformation("Director stopped");
    }

    private async Task RunMatchCycleAsync(CancellationToken ct)
    {
        var tasks = settings.Queues.Select(kv => FetchAndAssignAsync(kv.Key, kv.Value, ct));
        await Task.WhenAll(tasks);
    }

    private async Task FetchAndAssignAsync(string queueName, QueueConfiguration queueConfig, CancellationToken ct)
    {
        var request = new FetchMatchesRequest
        {
            Config = new FunctionConfig
            {
                Host = settings.MatchFunction.Host,
                Port = settings.MatchFunction.Port,
                Type = FunctionConfig.Types.Type.Grpc
            },
            Profile = BuildProfile(queueName, queueConfig)
        };

        try
        {
            using var stream = backendClient.FetchMatches(request, cancellationToken: ct);

            await foreach (var response in stream.ResponseStream.ReadAllAsync(ct))
            {
                if (response.Match is not null)
                    await ProcessMatchAsync(response.Match, queueName, queueConfig, ct);
            }
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.Unavailable)
        {
            logger.LogWarning("Backend unavailable for queue {Queue}, will retry next cycle", queueName);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error fetching matches for queue {Queue}", queueName);
        }
    }

    private async Task ProcessMatchAsync(Match match, string queueName, QueueConfiguration queueConfig, CancellationToken ct)
    {
        logger.LogInformation("Match {MatchId}: {TicketCount} ticket(s) for {Queue}",
            match.MatchId, match.Tickets.Count, queueName);

        var allocation = await allocator.AllocateAsync(queueName, queueConfig, match, ct);
        if (allocation is null)
        {
            logger.LogWarning("No server for match {MatchId}, releasing tickets", match.MatchId);
            await ReleaseTicketsAsync(match, ct);
            return;
        }

        var assignment = new Assignment { Connection = allocation.Connection };

        if (match.Extensions.TryGetValue(WellKnown.Extensions.PlayerCountKey, out var pcAny))
            assignment.Extensions[WellKnown.Extensions.PlayerCountKey] = pcAny;

        var ticketIds = match.Tickets.Select(t => t.Id).ToList();
        var response = await backendClient.AssignTicketsAsync(new AssignTicketsRequest
        {
            Assignments =
            {
                new AssignmentGroup
                {
                    Assignment = assignment,
                    TicketIds = { ticketIds }
                }
            }
        }, cancellationToken: ct);

        if (response.Failures.Count > 0)
        {
            foreach (var f in response.Failures)
                logger.LogWarning("Assign failed for ticket {TicketId}: {Cause}", f.TicketId, f.Cause);
        }
        else
        {
            logger.LogInformation("Assigned {Count} ticket(s) to {Connection} for {MatchId}",
                ticketIds.Count, allocation.Connection, match.MatchId);
        }
    }

    private async Task ReleaseTicketsAsync(Match match, CancellationToken ct)
    {
        try
        {
            await backendClient.ReleaseTicketsAsync(
                new ReleaseTicketsRequest { TicketIds = { match.Tickets.Select(t => t.Id) } },
                cancellationToken: ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error releasing tickets for match {MatchId}", match.MatchId);
        }
    }

    private static MatchProfile BuildProfile(string queueName, QueueConfiguration queueConfig) => new()
    {
        Name = queueName,
        Pools =
        {
            new Pool
            {
                Name = $"pool_{queueName}",
                TagPresentFilters = { new TagPresentFilter { Tag = queueConfig.Tag } }
            }
        }
    };
}
