using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using OpenMatch;
using SkullsLudo.Shared.Constants;

namespace SkullsLudo.Evaluator.Services;

public sealed class EvaluatorService(ILogger<EvaluatorService> logger)
    : OpenMatch.Evaluator.EvaluatorBase
{
    public override async Task Evaluate(
        IAsyncStreamReader<EvaluateRequest> requestStream,
        IServerStreamWriter<EvaluateResponse> responseStream,
        ServerCallContext context)
    {
        var proposals = new List<Match>();

        await foreach (var request in requestStream.ReadAllAsync(context.CancellationToken))
        {
            if (request.Match is not null)
                proposals.Add(request.Match);
        }

        logger.LogInformation("Evaluating {ProposalCount} match proposal(s)", proposals.Count);

        var approved = ResolveCollisions(proposals);

        logger.LogInformation("Approved {ApprovedCount} match(es) after de-collision", approved.Count);

        foreach (var match in approved)
        {
            await responseStream.WriteAsync(new EvaluateResponse { MatchId = match.MatchId });
        }
    }

    private List<Match> ResolveCollisions(List<Match> proposals)
    {
        var ticketToMatches = new Dictionary<string, List<Match>>();

        foreach (var match in proposals)
        {
            foreach (var ticket in match.Tickets)
            {
                if (!ticketToMatches.TryGetValue(ticket.Id, out var matchList))
                {
                    matchList = [];
                    ticketToMatches[ticket.Id] = matchList;
                }
                matchList.Add(match);
            }
        }

        var hasCollision = ticketToMatches.Values.Any(m => m.Count > 1);
        if (!hasCollision)
            return proposals;

        var rejected = new HashSet<string>();
        var collisionGroups = ticketToMatches
            .Where(kv => kv.Value.Count > 1)
            .Select(kv => kv.Value)
            .ToList();

        foreach (var group in collisionGroups)
        {
            var bestMatch = group
                .Where(m => !rejected.Contains(m.MatchId))
                .OrderByDescending(GetScore)
                .FirstOrDefault();

            if (bestMatch is null)
                continue;

            foreach (var match in group)
            {
                if (match.MatchId != bestMatch.MatchId)
                {
                    rejected.Add(match.MatchId);
                    logger.LogDebug("Rejected match {MatchId} (score {Score:F4}) in favor of {BestMatchId} (score {BestScore:F4})",
                        match.MatchId, GetScore(match), bestMatch.MatchId, GetScore(bestMatch));
                }
            }
        }

        return proposals.Where(m => !rejected.Contains(m.MatchId)).ToList();
    }

    private static double GetScore(Match match)
    {
        if (match.Extensions.TryGetValue(WellKnown.Extensions.ScoreKey, out var scoreAny))
        {
            var wrapped = scoreAny.Unpack<DoubleValue>();
            return wrapped.Value;
        }
        return 0;
    }
}
