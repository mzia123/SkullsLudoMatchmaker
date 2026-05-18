using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Agones.Allocation;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using OpenMatch;
using SkullsLudo.Shared.Configuration;
using SkullsLudo.Shared.Constants;

namespace SkullsLudo.Director.Services;

/// <summary>
/// Allocates an Agones GameServer from the configured fleet and stamps per-match
/// info onto the GameServer as annotations. The Unity build reads those annotations
/// via the Agones SDK at session start.
/// </summary>
public sealed class AgonesAllocatorService : IGameServerAllocator
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private readonly AgonesSettings _settings;
    private readonly ILogger<AgonesAllocatorService> _logger;
    private readonly AllocationService.AllocationServiceClient _client;

    public AgonesAllocatorService(
        MatchmakerSettings matchmakerSettings,
        AllocationService.AllocationServiceClient client,
        ILogger<AgonesAllocatorService> logger)
    {
        _settings = matchmakerSettings.Agones;
        _client = client;
        _logger = logger;
    }

    public static SocketsHttpHandler CreateHttpHandler(AgonesSettings settings, ILogger logger) =>
        new()
        {
            EnableMultipleHttp2Connections = true,
            SslOptions = BuildSslOptions(settings, logger)
        };

    public async Task<GameServerAllocation?> AllocateAsync(
        string queueName,
        QueueConfiguration queueConfig,
        Match match,
        CancellationToken ct = default)
    {
        var playerCount = ReadPlayerCount(match, queueConfig);
        var npcCount = Math.Max(0, queueConfig.MaxPlayers - playerCount);
        var prefix = _settings.AnnotationPrefix;

        var matchJson = JsonSerializer.Serialize(new MatchInfoPayload(
            match.MatchId, queueName, playerCount, npcCount), JsonOptions);

        var request = new AllocationRequest
        {
            Namespace = _settings.Namespace,
            Scheduling = AllocationRequest.Types.SchedulingStrategy.Packed,
            GameServerSelectors =
            {
                new GameServerSelector
                {
                    MatchLabels = { ["agones.dev/fleet"] = _settings.FleetName },
                    GameServerState = GameServerSelector.Types.GameServerState.Ready
                }
            },
            Metadata = new MetaPatch
            {
                Annotations =
                {
                    [$"{prefix}/match-id"] = match.MatchId,
                    [$"{prefix}/queue-name"] = queueName,
                    [$"{prefix}/player-count"] = playerCount.ToString(),
                    [$"{prefix}/npc-count"] = npcCount.ToString(),
                    [$"{prefix}/match-json"] = matchJson,
                    [$"{prefix}/ticket-ids"] = string.Join(",", match.Tickets.Select(t => t.Id))
                }
            }
        };

        try
        {
            var deadline = DateTime.UtcNow.AddSeconds(_settings.AllocationTimeoutSeconds);
            var response = await _client.AllocateAsync(request, deadline: deadline, cancellationToken: ct);

            if (string.IsNullOrEmpty(response.Address) || response.Ports.Count == 0)
            {
                _logger.LogWarning("Allocator returned empty address/ports for match {MatchId}", match.MatchId);
                return null;
            }

            var port = response.Ports[0].Port;
            _logger.LogInformation(
                "Allocated GameServer {Name} at {Address}:{Port} for match {MatchId} (queue={Queue}, players={Players}, npcs={Npcs})",
                response.GameServerName, response.Address, port, match.MatchId, queueName, playerCount, npcCount);

            return new GameServerAllocation
            {
                Address = response.Address,
                Port = port,
                GameServerName = response.GameServerName
            };
        }
        catch (RpcException ex) when (ex.StatusCode is StatusCode.ResourceExhausted)
        {
            _logger.LogWarning("No Ready GameServer in fleet {Fleet} for match {MatchId}",
                _settings.FleetName, match.MatchId);
            return null;
        }
        catch (RpcException ex) when (ex.StatusCode is StatusCode.DeadlineExceeded or StatusCode.Unavailable)
        {
            _logger.LogWarning("Allocator transient failure ({Code}) for match {MatchId}: {Message}",
                ex.StatusCode, match.MatchId, ex.Status.Detail);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to allocate GameServer for match {MatchId}", match.MatchId);
            return null;
        }
    }

    private static int ReadPlayerCount(Match match, QueueConfiguration queueConfig)
    {
        if (match.Extensions.TryGetValue(WellKnown.Extensions.PlayerCountKey, out var any))
            return any.Unpack<Int32Value>().Value;
        return queueConfig.MaxPlayers;
    }

    private static SslClientAuthenticationOptions BuildSslOptions(AgonesSettings s, ILogger logger)
    {
        var clientCert = LoadClientCertificate(s);
        var trustedCa = LoadCa(s);

        return new SslClientAuthenticationOptions
        {
            ClientCertificates = [clientCert],
            RemoteCertificateValidationCallback = (_, cert, chain, errors) =>
            {
                if (cert is null || chain is null)
                {
                    logger.LogError("Allocator TLS validation: missing cert/chain");
                    return false;
                }

                chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
                chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
                chain.ChainPolicy.CustomTrustStore.Clear();
                chain.ChainPolicy.CustomTrustStore.Add(trustedCa);

                var ok = chain.Build(new X509Certificate2(cert));
                if (!ok)
                    logger.LogError("Allocator TLS validation failed: errors={Errors}, status={Status}",
                        errors,
                        string.Join(", ", chain.ChainStatus.Select(s => s.StatusInformation.Trim())));
                return ok;
            }
        };
    }

    private static X509Certificate2 LoadClientCertificate(AgonesSettings s)
    {
        var pem = X509Certificate2.CreateFromPemFile(s.ClientCertPath, s.ClientKeyPath);
        // Round-trip via PKCS#12 so the private key is correctly associated for SslStream
        // on all platforms (Linux containers + Windows dev boxes).
        var pfx = pem.Export(X509ContentType.Pkcs12);
        return X509CertificateLoader.LoadPkcs12(pfx, password: null);
    }

    private static X509Certificate2 LoadCa(AgonesSettings s)
        => X509CertificateLoader.LoadCertificateFromFile(s.ServerCaPath);

    private sealed record MatchInfoPayload(
        string MatchId,
        string QueueName,
        int PlayerCount,
        int NpcCount);
}
