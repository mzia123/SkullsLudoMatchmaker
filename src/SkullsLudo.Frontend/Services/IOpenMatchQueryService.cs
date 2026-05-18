namespace SkullsLudo.Frontend.Services;

public interface IOpenMatchQueryService
{
    Task<bool> HasActiveTicketForPlayerAsync(string playerId, CancellationToken cancellationToken = default);
}
