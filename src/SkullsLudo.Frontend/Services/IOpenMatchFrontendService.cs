using OpenMatch;

namespace SkullsLudo.Frontend.Services;

public interface IOpenMatchFrontendService
{
    Task<Ticket> CreateTicketAsync(Ticket ticket, CancellationToken ct = default);
    Task<Ticket?> GetTicketAsync(string ticketId, CancellationToken ct = default);
    Task DeleteTicketAsync(string ticketId, CancellationToken ct = default);
}
