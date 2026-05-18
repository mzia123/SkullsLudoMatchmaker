using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SkullsLudo.Shared.Configuration;

namespace SkullsLudo.Frontend.Auth;

public sealed class ConfigureUnityJwtBearerOptions(
    MatchmakerSettings settings,
    IUnityJwksProvider jwksProvider) : IConfigureNamedOptions<JwtBearerOptions>
{
    public void Configure(string? name, JwtBearerOptions options) =>
        Configure(options);

    public void Configure(JwtBearerOptions options)
    {
        var unityAuth = settings.UnityAuth;

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = unityAuth.ValidIssuer,
            ValidateAudience = !string.IsNullOrEmpty(unityAuth.ValidAudience),
            ValidAudience = unityAuth.ValidAudience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            NameClaimType = unityAuth.PlayerIdClaim,
            IssuerSigningKeyResolver = (_, _, kid, _) =>
                jwksProvider.GetSigningKeysAsync(kid).GetAwaiter().GetResult()
        };

        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = context =>
            {
                if (string.IsNullOrEmpty(unityAuth.ValidProjectId))
                    return Task.CompletedTask;

                var projectId = context.Principal?.FindFirstValue("project_id");
                if (!string.Equals(projectId, unityAuth.ValidProjectId, StringComparison.Ordinal))
                    context.Fail("Invalid project_id claim.");

                return Task.CompletedTask;
            }
        };
    }
}
