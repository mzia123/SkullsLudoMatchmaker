using Google.Protobuf.WellKnownTypes;
using Microsoft.AspNetCore.Mvc;
using OpenMatch;
using SkullsLudo.Frontend.Auth;
using SkullsLudo.Frontend.Models;
using SkullsLudo.Frontend.Services;
using SkullsLudo.Shared.Configuration;
using SkullsLudo.Shared.Constants;

namespace SkullsLudo.Frontend.Endpoints;

public static class MatchmakingEndpoints
{
    public const string CreateTicketRateLimitPolicy = "create-ticket";

    public static RouteGroupBuilder MapMatchmakingEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/matchmaking/tickets")
            .WithTags("Matchmaking")
            .RequireAuthorization();

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
        HttpContext httpContext,
        [FromBody] Models.CreateTicketRequest request,
        [FromServices] IOpenMatchFrontendService frontendService,
        [FromServices] IOpenMatchQueryService queryService,
        [FromServices] MatchmakerSettings matchmaker,
        [FromServices] PlayerIdResolver playerIdResolver,
        CancellationToken ct)
    {
        if (!playerIdResolver.TryResolve(httpContext, out var playerId, out var authError))
            return authError!;

        if (!matchmaker.Queues.TryGetValue(request.Queue, out var queueConfig))
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["queue"] = [$"Unknown queue '{request.Queue}'. Valid: {string.Join(", ", matchmaker.Queues.Keys)}"]
            });

        if (await queryService.HasActiveTicketForPlayerAsync(playerId, ct))
        {
            return Results.Conflict(new
            {
                error = "Player already has an active matchmaking ticket. Cancel it before creating a new one."
            });
        }

        var ticket = new Ticket
        {
            SearchFields = new SearchFields
            {
                DoubleArgs = { { WellKnown.SearchFields.Mmr, request.Mmr } },
                StringArgs = { { WellKnown.SearchFields.PlayerId, playerId } },
                Tags = { queueConfig.Tag }
            }
        };

        var created = await frontendService.CreateTicketAsync(ticket, ct);

        return Results.Created($"/api/matchmaking/tickets/{created.Id}",
            new CreateTicketResponse { TicketId = created.Id });
    }

    private static async Task<IResult> GetTicketStatus(
        HttpContext httpContext,
        string ticketId,
        [FromServices] IOpenMatchFrontendService frontendService,
        [FromServices] MatchmakerSettings matchmaker,
        [FromServices] PlayerIdResolver playerIdResolver,
        CancellationToken ct)
    {
        if (!playerIdResolver.TryResolve(httpContext, out var playerId, out var authError))
            return authError!;

        var ticket = await frontendService.GetTicketAsync(ticketId, ct);
        if (ticket is null || !OwnsTicket(ticket, playerId))
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
        HttpContext httpContext,
        string ticketId,
        [FromServices] IOpenMatchFrontendService frontendService,
        [FromServices] PlayerIdResolver playerIdResolver,
        CancellationToken ct)
    {
        if (!playerIdResolver.TryResolve(httpContext, out var playerId, out var authError))
            return authError!;

        var ticket = await frontendService.GetTicketAsync(ticketId, ct);
        if (ticket is null || !OwnsTicket(ticket, playerId))
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

    private static bool OwnsTicket(Ticket ticket, string playerId) =>
        ticket.SearchFields.StringArgs.TryGetValue(WellKnown.SearchFields.PlayerId, out var owner)
        && string.Equals(owner, playerId, StringComparison.Ordinal);
}
