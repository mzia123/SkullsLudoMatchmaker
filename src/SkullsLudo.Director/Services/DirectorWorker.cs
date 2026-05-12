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
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Director started. Loop interval: {Interval}ms", settings.Director.LoopIntervalMs);

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
                logger.LogError(ex, "Error in director match cycle");
            }

            await Task.Delay(settings.Director.LoopIntervalMs, stoppingToken);
        }

        logger.LogInformation("Director stopping");
    }

    private async Task RunMatchCycleAsync(CancellationToken ct)
    {
        var fetchTasks = settings.Queues.Select(kv => FetchAndAssignAsync(kv.Key, kv.Value, ct));
        await Task.WhenAll(fetchTasks);
    }

    private async Task FetchAndAssignAsync(string queueName, QueueConfiguration queueConfig, CancellationToken ct)
    {
        var profile = BuildProfile(queueName, queueConfig);
        var functionConfig = new FunctionConfig
        {
            Host = settings.MatchFunction.Host,
            Port = settings.MatchFunction.Port,
            Type = FunctionConfig.Types.Type.Grpc
        };

        var request = new FetchMatchesRequest
        {
            Config = functionConfig,
            Profile = profile
        };

        try
        {
            using var stream = backendClient.FetchMatches(request, cancellationToken: ct);

            await foreach (var response in stream.ResponseStream.ReadAllAsync(ct))
            {
                if (response.Match is null)
                    continue;

                await ProcessMatchAsync(response.Match, queueName, ct);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error fetching matches for queue {Queue}", queueName);
        }
    }

    private async Task ProcessMatchAsync(Match match, string queueName, CancellationToken ct)
    {
        logger.LogInformation("Processing match {MatchId} with {TicketCount} ticket(s) for queue {Queue}",
            match.MatchId, match.Tickets.Count, queueName);

        var allocation = await allocator.AllocateAsync(queueName, ct);
        if (allocation is null)
        {
            logger.LogWarning("Failed to allocate server for match {MatchId}. Releasing tickets.", match.MatchId);
            await ReleaseTicketsAsync(match, ct);
            return;
        }

        var ticketIds = match.Tickets.Select(t => t.Id).ToList();
        var assignment = new Assignment { Connection = allocation.Connection };

        if (match.Extensions.TryGetValue(WellKnown.Extensions.PlayerCountKey, out var pcAny))
            assignment.Extensions[WellKnown.Extensions.PlayerCountKey] = pcAny;

        var assignRequest = new AssignTicketsRequest
        {
            Assignments =
            {
                new AssignmentGroup
                {
                    Assignment = assignment,
                    TicketIds = { ticketIds }
                }
            }
        };

        var assignResponse = await backendClient.AssignTicketsAsync(assignRequest, cancellationToken: ct);

        if (assignResponse.Failures.Count > 0)
        {
            foreach (var failure in assignResponse.Failures)
            {
                logger.LogWarning("Failed to assign ticket {TicketId}: {Cause}", failure.TicketId, failure.Cause);
            }
        }
        else
        {
            logger.LogInformation("Assigned {TicketCount} ticket(s) to {Connection} for match {MatchId}",
                ticketIds.Count, allocation.Connection, match.MatchId);
        }
    }

    private async Task ReleaseTicketsAsync(Match match, CancellationToken ct)
    {
        try
        {
            var ticketIds = match.Tickets.Select(t => t.Id).ToList();
            await backendClient.ReleaseTicketsAsync(
                new ReleaseTicketsRequest { TicketIds = { ticketIds } },
                cancellationToken: ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error releasing tickets for match {MatchId}", match.MatchId);
        }
    }

    private static MatchProfile BuildProfile(string queueName, QueueConfiguration queueConfig)
    {
        var pool = new Pool
        {
            Name = $"pool_{queueName}",
            TagPresentFilters = { new TagPresentFilter { Tag = queueConfig.Tag } }
        };

        return new MatchProfile
        {
            Name = queueName,
            Pools = { pool }
        };
    }
}
