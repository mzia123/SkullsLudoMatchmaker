using Google.Protobuf.WellKnownTypes;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using OpenMatch;
using SkullsLudo.Frontend.Models;
using SkullsLudo.Frontend.Services;
using SkullsLudo.Shared.Configuration;
using SkullsLudo.Shared.Constants;

namespace SkullsLudo.Frontend.Endpoints;

public static class MatchmakingEndpoints
{
    public const string CreateTicketRateLimitPolicy = "create-ticket";

    private const string IdempotencyCachePrefix = "idemp:";

    public static RouteGroupBuilder MapMatchmakingEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/matchmaking/tickets")
            .WithTags("Matchmaking");

        group.MapPost("/", CreateTicket)
            .WithName("CreateTicket")
            .RequireRateLimiting(CreateTicketRateLimitPolicy)
            .Produces<CreateTicketResponse>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status409Conflict)
            .Produces(StatusCodes.Status429TooManyRequests)
            .ProducesValidationProblem();

        group.MapGet("/{ticketId}", GetTicketStatus)
            .WithName("GetTicketStatus")
            .Produces<TicketStatusResponse>()
            .Produces(StatusCodes.Status404NotFound);

        group.MapDelete("/{ticketId}", CancelTicket)
            .WithName("CancelTicket")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict);

        return group;
    }

    private static async Task<IResult> CreateTicket(
        HttpRequest httpRequest,
        [FromBody] Models.CreateTicketRequest request,
        [FromServices] IOpenMatchFrontendService frontendService,
        [FromServices] MatchmakerSettings matchmaker,
        [FromServices] IMemoryCache cache,
        CancellationToken ct)
    {
        if (!matchmaker.Queues.TryGetValue(request.Queue, out var queueConfig))
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["queue"] = [$"Unknown queue '{request.Queue}'. Valid: {string.Join(", ", matchmaker.Queues.Keys)}"]
            });

        if (httpRequest.Headers.TryGetValue("Idempotency-Key", out var idemValues))
        {
            var idemKey = idemValues.ToString().Trim();
            if (idemKey.Length > 0)
            {
                var cacheKey = IdempotencyCachePrefix + idemKey;
                if (cache.TryGetValue(cacheKey, out IdempotencyTicketRecord? prior) && prior is not null)
                {
                    if (!string.Equals(prior.Queue, request.Queue, StringComparison.Ordinal)
                        || !string.Equals(prior.PlayerId, request.PlayerId, StringComparison.Ordinal)
                        || Math.Abs(prior.Mmr - request.Mmr) > double.Epsilon)
                    {
                        return Results.Conflict(new { error = "Idempotency-Key reused with a different payload." });
                    }

                    return Results.Created($"/api/matchmaking/tickets/{prior.TicketId}",
                        new CreateTicketResponse { TicketId = prior.TicketId });
                }
            }
        }

        var ticket = new Ticket
        {
            SearchFields = new SearchFields
            {
                DoubleArgs = { { WellKnown.SearchFields.Mmr, request.Mmr } },
                StringArgs = { { WellKnown.SearchFields.PlayerId, request.PlayerId } },
                Tags = { queueConfig.Tag }
            }
        };

        var created = await frontendService.CreateTicketAsync(ticket, ct);

        if (httpRequest.Headers.TryGetValue("Idempotency-Key", out var idemHeader))
        {
            var idemKey = idemHeader.ToString().Trim();
            if (idemKey.Length > 0)
            {
                cache.Set(
                    IdempotencyCachePrefix + idemKey,
                    new IdempotencyTicketRecord(created.Id, request.Queue, request.PlayerId, request.Mmr),
                    new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1) });
            }
        }

        return Results.Created($"/api/matchmaking/tickets/{created.Id}",
            new CreateTicketResponse { TicketId = created.Id });
    }

    private static async Task<IResult> GetTicketStatus(
        string ticketId,
        [FromServices] IOpenMatchFrontendService frontendService,
        [FromServices] MatchmakerSettings matchmaker,
        CancellationToken ct)
    {
        var ticket = await frontendService.GetTicketAsync(ticketId, ct);
        if (ticket is null)
            return Results.NotFound();

        if (ticket.Assignment is { Connection.Length: > 0 } assignment)
        {
            int? playerCount = null;
            if (assignment.Extensions.TryGetValue(WellKnown.Extensions.PlayerCountKey, out var pcAny))
                playerCount = pcAny.Unpack<Int32Value>().Value;

            return Results.Ok(new TicketStatusResponse
            {
                TicketId = ticket.Id,
                Status = TicketStatus.Matched,
                Connection = assignment.Connection,
                PlayerCount = playerCount
            });
        }

        if (ticket.Assignment?.Extensions.ContainsKey(WellKnown.Extensions.TimeoutKey) == true)
        {
            await frontendService.DeleteTicketAsync(ticketId, ct);
            return Results.Ok(new TicketStatusResponse
            {
                TicketId = ticket.Id,
                Status = TicketStatus.Timeout
            });
        }

        var ticketAge = DateTime.UtcNow - ticket.CreateTime.ToDateTime();
        var searchTimeout = TimeSpan.FromSeconds(matchmaker.Frontend.TicketSearchTimeoutSeconds);
        if (ticketAge > searchTimeout)
        {
            await frontendService.DeleteTicketAsync(ticketId, ct);
            return Results.Ok(new TicketStatusResponse
            {
                TicketId = ticket.Id,
                Status = TicketStatus.Timeout
            });
        }

        return Results.Ok(new TicketStatusResponse
        {
            TicketId = ticket.Id,
            Status = TicketStatus.Searching
        });
    }

    private static async Task<IResult> CancelTicket(
        string ticketId,
        [FromServices] IOpenMatchFrontendService frontendService,
        CancellationToken ct)
    {
        var ticket = await frontendService.GetTicketAsync(ticketId, ct);
        if (ticket is null)
            return Results.NotFound();

        if (ticket.Assignment is { Connection.Length: > 0 })
        {
            return Results.Conflict(new
            {
                error = "Ticket is already matched; cancel is not allowed."
            });
        }

        await frontendService.DeleteTicketAsync(ticketId, ct);
        return Results.NoContent();
    }

    private sealed record IdempotencyTicketRecord(string TicketId, string Queue, string PlayerId, double Mmr);
}
