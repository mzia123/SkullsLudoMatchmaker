using Grpc.Core;
using OpenMatch;
using SkullsLudo.MatchFunction.Strategies;

namespace SkullsLudo.MatchFunction.Services;

public sealed class MatchFunctionService(
    MatchStrategyResolver strategyResolver,
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

        var strategy = strategyResolver.Resolve(profile.Name);
        if (strategy is null)
        {
            logger.LogWarning("No strategy registered for profile {ProfileName}. Registered: [{Registered}]",
                profile.Name, string.Join(", ", strategyResolver.RegisteredQueues));
            return;
        }

        var poolTickets = await QueryPoolsAsync(profile, context.CancellationToken);

        var totalTickets = poolTickets.Values.Sum(t => t.Count);
        logger.LogInformation("Queried {TotalTickets} ticket(s) across {PoolCount} pool(s) for {ProfileName}",
            totalTickets, poolTickets.Count, profile.Name);

        if (totalTickets == 0)
            return;

        var matches = strategy.CreateMatches(profile, poolTickets);
        logger.LogInformation("Strategy {Strategy} produced {MatchCount} match proposal(s) for {ProfileName}",
            strategy.GetType().Name, matches.Count, profile.Name);

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
