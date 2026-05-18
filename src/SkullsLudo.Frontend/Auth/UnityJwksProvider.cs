using Microsoft.IdentityModel.Tokens;
using SkullsLudo.Shared.Configuration;

namespace SkullsLudo.Frontend.Auth;

public sealed class UnityJwksProvider(
    IHttpClientFactory httpClientFactory,
    MatchmakerSettings settings,
    ILogger<UnityJwksProvider> logger) : IUnityJwksProvider
{
    private readonly SemaphoreSlim _lock = new(1, 1);
    private JsonWebKeySet? _jwks;
    private DateTimeOffset _fetchedAt;

    public async Task<IReadOnlyList<SecurityKey>> GetSigningKeysAsync(string? kid, CancellationToken cancellationToken = default)
    {
        await EnsureFreshAsync(forceRefresh: false, cancellationToken).ConfigureAwait(false);

        if (kid is not null && !KeyIdExists(kid))
        {
            logger.LogInformation("JWKS kid {Kid} not in cache; refreshing", kid);
            await EnsureFreshAsync(forceRefresh: true, cancellationToken).ConfigureAwait(false);
        }

        return _jwks?.GetSigningKeys().ToList() ?? [];
    }

    private bool KeyIdExists(string kid) =>
        _jwks?.GetSigningKeys().Any(k => string.Equals(k.KeyId, kid, StringComparison.Ordinal)) == true;

    private async Task EnsureFreshAsync(bool forceRefresh, CancellationToken cancellationToken)
    {
        var ttl = TimeSpan.FromHours(settings.UnityAuth.JwksCacheTtlHours);
        if (!forceRefresh && _jwks is not null && DateTimeOffset.UtcNow - _fetchedAt < ttl)
            return;

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!forceRefresh && _jwks is not null && DateTimeOffset.UtcNow - _fetchedAt < ttl)
                return;

            var uri = settings.UnityAuth.JwksUri;
            logger.LogInformation("Fetching Unity JWKS from {Uri}", uri);

            using var client = httpClientFactory.CreateClient(nameof(UnityJwksProvider));
            var json = await client.GetStringAsync(uri, cancellationToken).ConfigureAwait(false);
            _jwks = new JsonWebKeySet(json);
            _fetchedAt = DateTimeOffset.UtcNow;
        }
        finally
        {
            _lock.Release();
        }
    }
}
