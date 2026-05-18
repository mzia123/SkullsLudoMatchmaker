using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using SkullsLudo.Shared.Configuration;

namespace SkullsLudo.Frontend.Auth;

public static class AuthDependencyInjection
{
    public static IServiceCollection AddSkullsLudoAuthentication(
        this IServiceCollection services,
        MatchmakerSettings settings)
    {
        services.AddSingleton<IUnityJwksProvider, UnityJwksProvider>();
        services.AddSingleton<PlayerIdResolver>();
        services.AddHttpClient(nameof(UnityJwksProvider));

        if (!settings.UnityAuth.Enabled)
        {
            services.AddAuthentication(DebugAuthenticationDefaults.Scheme)
                .AddScheme<AuthenticationSchemeOptions, DebugAuthenticationHandler>(
                    DebugAuthenticationDefaults.Scheme,
                    _ => { });
            return services;
        }

        services.AddSingleton<IConfigureNamedOptions<JwtBearerOptions>, ConfigureUnityJwtBearerOptions>();
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer();

        return services;
    }
}
