using Grpc.Core;
using OpenMatch;

namespace SkullsLudo.Frontend.Services;

public sealed class OpenMatchFrontendService(
    FrontendService.FrontendServiceClient grpcClient,
    ILogger<OpenMatchFrontendService> logger) : IOpenMatchFrontendService
{
    public async Task<Ticket> CreateTicketAsync(Ticket ticket, CancellationToken ct = default)
    {
        var request = new CreateTicketRequest { Ticket = ticket };
        var created = await grpcClient.CreateTicketAsync(request, cancellationToken: ct);
        logger.LogInformation("Created ticket {TicketId} for queue tag(s): {Tags}",
            created.Id, string.Join(", ", ticket.SearchFields.Tags));
        return created;
    }

    public async Task<Ticket?> GetTicketAsync(string ticketId, CancellationToken ct = default)
    {
        try
        {
            var request = new GetTicketRequest { TicketId = ticketId };
            return await grpcClient.GetTicketAsync(request, cancellationToken: ct);
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task DeleteTicketAsync(string ticketId, CancellationToken ct = default)
    {
        try
        {
            var request = new DeleteTicketRequest { TicketId = ticketId };
            await grpcClient.DeleteTicketAsync(request, cancellationToken: ct);
            logger.LogInformation("Deleted ticket {TicketId}", ticketId);
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
        {
            logger.LogWarning("Attempted to delete non-existent ticket {TicketId}", ticketId);
        }
    }
}
