using Grpc.Core;
using OpenMatch;
using SkullsLudo.Shared.Constants;

namespace SkullsLudo.Frontend.Services;

public sealed class OpenMatchQueryService(
    QueryService.QueryServiceClient queryClient,
    ILogger<OpenMatchQueryService> logger) : IOpenMatchQueryService
{
    public async Task<bool> HasActiveTicketForPlayerAsync(string playerId, CancellationToken cancellationToken = default)
    {
        var pool = new Pool
        {
            Name = $"player_{playerId}",
            StringEqualsFilters =
            {
                new StringEqualsFilter
                {
                    StringArg = WellKnown.SearchFields.PlayerId,
                    Value = playerId
                }
            }
        };

        try
        {
            using var call = queryClient.QueryTickets(new QueryTicketsRequest { Pool = pool }, cancellationToken: cancellationToken);
            await foreach (var response in call.ResponseStream.ReadAllAsync(cancellationToken))
            {
                if (response.Tickets.Count > 0)
                    return true;
            }
        }
        catch (RpcException ex)
        {
            logger.LogError(ex, "Open Match query failed for player {PlayerId}", playerId);
            throw;
        }

        return false;
    }
}
