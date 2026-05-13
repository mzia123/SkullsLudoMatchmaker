using Grpc.Core;
using OpenMatch;
using SkullsLudo.MatchFunction.Strategies;
using SkullsLudo.Shared.Configuration;

namespace SkullsLudo.MatchFunction.Services;

public sealed class MatchFunctionService(
    MatchStrategyResolver strategyResolver,
    MatchmakerSettings settings,
    QueryService.QueryServiceClient queryClient,
    ILogger<MatchFunctionService> logger) : OpenMatch.MatchFunction.MatchFunctionBase
{
    public override async Task Run(
        RunRequest request,
        IServerStreamWriter<RunResponse> responseStream,
        ServerCallContext context)
    {
        var profile = request.Profile;
        logger.LogInformation("MatchFunction invoked for profile {ProfileName} with {PoolCount} pool(s)",
            profile.Name, profile.Pools.Count);

        // Profile name is the queue name (Director sets it that way). Look up the
        // queue, then resolve its configured strategy. Multiple queues may share a
        // strategy with different parameters.
        if (!settings.Queues.TryGetValue(profile.Name, out var queueConfig))
        {
            logger.LogWarning("No queue configuration for profile {ProfileName}. Known: [{Known}]",
                profile.Name, string.Join(", ", settings.Queues.Keys));
            return;
        }

        var strategy = strategyResolver.Resolve(queueConfig.Strategy);
        if (strategy is null)
        {
            logger.LogWarning(
                "Queue {Queue} references unknown strategy '{Strategy}'. Registered: [{Registered}]",
                profile.Name, queueConfig.Strategy, string.Join(", ", strategyResolver.RegisteredStrategies));
            return;
        }

        var poolTickets = await QueryPoolsAsync(profile, context.CancellationToken);

        var totalTickets = poolTickets.Values.Sum(t => t.Count);
        logger.LogInformation(
            "Queried {TotalTickets} ticket(s) across {PoolCount} pool(s) for {ProfileName} (strategy={Strategy})",
            totalTickets, poolTickets.Count, profile.Name, strategy.Name);

        if (totalTickets == 0)
            return;

        var matches = strategy.CreateMatches(profile, poolTickets, queueConfig);
        logger.LogInformation(
            "Strategy {Strategy} produced {MatchCount} match proposal(s) for {ProfileName}",
            strategy.Name, matches.Count, profile.Name);

        foreach (var match in matches)
        {
            await responseStream.WriteAsync(new RunResponse { Proposal = match });
        }
    }

    private async Task<IDictionary<string, IList<Ticket>>> QueryPoolsAsync(
        MatchProfile profile,
        CancellationToken ct)
    {
        var result = new Dictionary<string, IList<Ticket>>();

        foreach (var pool in profile.Pools)
        {
            var tickets = new List<Ticket>();
            var request = new QueryTicketsRequest { Pool = pool };

            using var stream = queryClient.QueryTickets(request, cancellationToken: ct);
            await foreach (var response in stream.ResponseStream.ReadAllAsync(ct))
            {
                tickets.AddRange(response.Tickets);
            }

            result[pool.Name] = tickets;
        }

        return result;
    }
}
