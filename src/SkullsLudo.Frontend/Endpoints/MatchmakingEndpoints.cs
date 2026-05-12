using Google.Protobuf.WellKnownTypes;
using Microsoft.AspNetCore.Mvc;
using OpenMatch;
using SkullsLudo.Frontend.Models;
using SkullsLudo.Frontend.Services;
using SkullsLudo.Shared.Configuration;
using SkullsLudo.Shared.Constants;

namespace SkullsLudo.Frontend.Endpoints;

public static class MatchmakingEndpoints
{
    public static RouteGroupBuilder MapMatchmakingEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/matchmaking/tickets")
            .WithTags("Matchmaking");

        group.MapPost("/", CreateTicket)
            .WithName("CreateTicket")
            .Produces<CreateTicketResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem();

        group.MapGet("/{ticketId}", GetTicketStatus)
            .WithName("GetTicketStatus")
            .Produces<TicketStatusResponse>()
            .Produces(StatusCodes.Status404NotFound);

        group.MapDelete("/{ticketId}", CancelTicket)
            .WithName("CancelTicket")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound);

        return group;
    }

    private static async Task<IResult> CreateTicket(
        [FromBody] Models.CreateTicketRequest request,
        [FromServices] IOpenMatchFrontendService frontendService,
        [FromServices] IConfiguration config,
        CancellationToken ct)
    {
        var queues = config.GetSection("Matchmaker:Queues")
            .Get<Dictionary<string, QueueConfiguration>>() ?? DefaultQueues.All;

        if (!queues.TryGetValue(request.Queue, out var queueConfig))
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["queue"] = [$"Unknown queue '{request.Queue}'. Valid queues: {string.Join(", ", queues.Keys)}"]
            });

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

        var response = new CreateTicketResponse { TicketId = created.Id };
        return Results.Created($"/api/matchmaking/tickets/{created.Id}", response);
    }

    private static async Task<IResult> GetTicketStatus(
        string ticketId,
        [FromServices] IOpenMatchFrontendService frontendService,
        [FromServices] IConfiguration config,
        CancellationToken ct)
    {
        var ticket = await frontendService.GetTicketAsync(ticketId, ct);
        if (ticket is null)
            return Results.NotFound();

        var timeoutMinutes = config.GetValue("Matchmaker:TimeoutMinutes", 2);
        var ticketAge = DateTime.UtcNow - ticket.CreateTime.ToDateTime();

        if (ticket.Assignment is { Connection.Length: > 0 } assignment)
        {
            int? playerCount = null;
            if (assignment.Extensions.TryGetValue(WellKnown.Extensions.PlayerCountKey, out var pcAny))
            {
                var wrappedValue = pcAny.Unpack<Int32Value>();
                playerCount = wrappedValue.Value;
            }

            return Results.Ok(new TicketStatusResponse
            {
                TicketId = ticket.Id,
                Status = TicketStatus.Matched,
                Connection = assignment.Connection,
                PlayerCount = playerCount
            });
        }

        if (ticket.Assignment is not null &&
            ticket.Assignment.Extensions.ContainsKey(WellKnown.Extensions.TimeoutKey))
        {
            await frontendService.DeleteTicketAsync(ticketId, ct);
            return Results.Ok(new TicketStatusResponse
            {
                TicketId = ticket.Id,
                Status = TicketStatus.Timeout
            });
        }

        if (ticketAge > TimeSpan.FromMinutes(timeoutMinutes))
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

        await frontendService.DeleteTicketAsync(ticketId, ct);
        return Results.NoContent();
    }
}
