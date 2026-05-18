using Microsoft.IdentityModel.Tokens;

namespace SkullsLudo.Frontend.Auth;

public interface IUnityJwksProvider
{
    Task<IReadOnlyList<SecurityKey>> GetSigningKeysAsync(string? kid, CancellationToken cancellationToken = default);
}
