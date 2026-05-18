using System.Security.Claims;
using SkullsLudo.Shared.Configuration;

namespace SkullsLudo.Frontend.Auth;

public sealed class PlayerIdResolver(MatchmakerSettings settings)
{
    public bool TryResolve(HttpContext httpContext, out string playerId, out IResult? errorResult)
    {
        errorResult = null;
        var auth = settings.UnityAuth;

        if (auth.Enabled)
        {
            playerId = httpContext.User.FindFirstValue(auth.PlayerIdClaim)
                ?? httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? string.Empty;

            if (playerId.Length == 0)
            {
                errorResult = Results.Unauthorized();
                return false;
            }

            return true;
        }

        if (httpContext.Request.Headers.TryGetValue(auth.DebugPlayerIdHeader, out var headerValues))
        {
            playerId = headerValues.ToString().Trim();
            if (playerId.Length > 0)
                return true;
        }

        playerId = auth.DefaultDebugPlayerId;
        return true;
    }
}
